using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Features.Agents;
using AiGateway.Api.Features.AppContext;
using AiGateway.Api.Features.Sessions;
using AiGateway.Api.Infrastructure.AiProviders;
using AiGateway.Api.Skills;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using System.ClientModel;
using System.Text.Json;
using GatewayResponse = AiGateway.Api.Core.Models.ChatResponse;

namespace AiGateway.Api.Features.Chat;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/chat/completions", async (
            ChatRequest request,
            ITaskAnalyzer analyzer,
            AgentSelector agentSelector,
            IProviderRegistry registry,
            IPromptEnhancer enhancer,
            ICostTracker costTracker,
            MemorySkill memorySkill,
            IAppContext appContext,
            ISessionStore sessionStore,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("AiGateway.Api.ChatEndpoint");

            if (!string.IsNullOrEmpty(request.AppId))
            {
                httpContext.Items["AppId"] = request.AppId;
            }

            if (request.Provider.HasValue && !registry.IsConfigured(request.Provider.Value))
            {
                return Results.Problem(
                    title: "Provider not configured",
                    detail: $"Provider '{request.Provider.Value}' is not configured on this gateway. Configure its API key or omit the field to let the gateway auto-select.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var sessionId = request.SessionId ?? httpContext.Request.Headers["X-Session-Id"].ToString();
                Session? session = null;
                bool sessionHit = false;

                if (!string.IsNullOrEmpty(sessionId))
                {
                    session = await sessionStore.GetAsync(sessionId);
                    if (session != null)
                    {
                        sessionHit = true;
                    }
                }

                var domain = request.Domain ?? session?.Domain;
                var complexity = request.Complexity ?? session?.Complexity;

                TaskAnalysis analysis;
                if (domain.HasValue && complexity.HasValue)
                {
                    analysis = new TaskAnalysis(domain.Value, complexity.Value);
                }
                else
                {
                    analysis = await analyzer.AnalyzeAsync(request.Prompt, cancellationToken);
                }

                var (decision, agentScope) = await agentSelector.SelectAsync(
                    analysis,
                    request.AgentId ?? session?.AgentId,
                    request.Provider ?? session?.Provider,
                    isPinned: request.Provider.HasValue);

                decision = RerouteForStructuredOutput(decision, request, registry, logger);

                var actualPrompt = request.Prompt;
                string? enhancedPrompt = null;

                if (request.EnablePromptEnhancement)
                {
                    var hint = agentSelector.GetEnhancementHint(analysis.Domain);
                    actualPrompt = await enhancer.EnhanceAsync(request.Prompt, hint, cancellationToken);
                    enhancedPrompt = actualPrompt;
                }

                var messages = new List<ChatMessage>();
                if (session != null)
                {
                    foreach (var turn in session.Turns)
                    {
                        messages.Add(new ChatMessage(new ChatRole(turn.Role), turn.Content));
                    }
                }
                messages.Add(new ChatMessage(ChatRole.User, actualPrompt));

                var tools = request.UseSkills && decision.RequiredSkills.Count > 0
                    ? BuildTools(decision.RequiredSkills, memorySkill)
                    : null;

                ChatResponseFormat? responseFormat = null;
                if (string.Equals(request.ResponseMimeType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    var schemaUnsupported = decision.Provider == AiProvider.Anthropic && request.ResponseSchema.HasValue;
                    if (schemaUnsupported)
                    {
                        logger.LogWarning(
                            "Anthropic's OpenAI-compatible endpoint does not support response_format json_schema. Falling back to plain JSON mode.");
                        responseFormat = ChatResponseFormat.Json;
                    }
                    else
                    {
                        responseFormat = request.ResponseSchema.HasValue
                            ? ChatResponseFormat.ForJsonSchema(request.ResponseSchema.Value)
                            : ChatResponseFormat.Json;
                    }
                }

                AiProvider providerUsed = decision.Provider;
                string modelUsed = string.Empty;

                var response = await registry.ExecuteAsync(
                    decision.Provider,
                    decision.Analysis.Complexity,
                    async ctx =>
                    {
                        providerUsed = ctx.Provider;
                        modelUsed = ctx.ModelName;

                        var optimized = new AgentOptimizationClient(
                            ctx.Client,
                            decision.SystemPromptFragment,
                            request.SystemInstruction);

                        var options = new ChatOptions { ModelId = ctx.ModelName };
                        if (tools is not null) options.Tools = tools;
                        if (responseFormat is not null) options.ResponseFormat = responseFormat;

                        return await optimized.GetResponseAsync(messages, options, cancellationToken);
                    },
                    allowFallback: !decision.IsProviderPinned,
                    cancellationToken: cancellationToken);

                if (!string.IsNullOrEmpty(sessionId))
                {
                    var ttlMinutes = configuration.GetValue("SESSION_TTL_MINUTES", 30);
                    var now = DateTime.UtcNow;

                    if (session == null)
                    {
                        session = new Session
                        {
                            Id = sessionId,
                            Domain = analysis.Domain,
                            Complexity = analysis.Complexity,
                            Provider = providerUsed,
                            AgentId = request.AgentId,
                            CreatedAt = now,
                            UpdatedAt = now,
                            ExpiresAt = now.AddMinutes(ttlMinutes),
                            Turns = new List<SessionTurn>()
                        };
                    }
                    else
                    {
                        session = session with
                        {
                            UpdatedAt = now,
                            ExpiresAt = now.AddMinutes(ttlMinutes)
                        };
                    }

                    session.Turns.Add(new SessionTurn { Role = "user", Content = request.Prompt, Timestamp = now });
                    session.Turns.Add(new SessionTurn { Role = "assistant", Content = response.Text ?? string.Empty, Timestamp = now });

                    if (session.Turns.Count > 6)
                    {
                        session.Turns.RemoveRange(0, session.Turns.Count - 6);
                    }

                    session = session with { TurnCount = session.Turns.Count };
                    await sessionStore.UpsertAsync(session);
                }

                var usage = response.Usage is not null
                    ? new TokenUsage(
                        response.Usage.InputTokenCount ?? 0,
                        response.Usage.OutputTokenCount ?? 0,
                        response.Usage.TotalTokenCount ?? 0)
                    : null;

                var estimatedCost = usage is not null
                    ? costTracker.EstimateCost(modelUsed, usage.InputTokens, usage.OutputTokens)
                    : (decimal?)null;

                return Results.Ok(new GatewayResponse
                {
                    Completion = response.Text ?? string.Empty,
                    ModelUsed = modelUsed,
                    ProviderUsed = providerUsed,
                    Domain = analysis.Domain,
                    EnhancedPrompt = enhancedPrompt,
                    Usage = usage,
                    EstimatedCost = estimatedCost,
                    SessionHit = sessionHit,
                    AppId = appContext.AppId,
                    AgentScope = agentScope
                });
            }
            catch (ProviderNotConfiguredException ex)
            {
                return Results.Problem(
                    title: "Provider not configured",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (ClientResultException ex)
            {
                logger.LogWarning(ex, "Pinned provider returned an error.");
                return Results.Problem(
                    title: "Provider error",
                    detail: ex.Message,
                    statusCode: ex.Status >= 400 && ex.Status < 600 ? ex.Status : StatusCodes.Status502BadGateway);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("All AI providers failed", StringComparison.Ordinal))
            {
                logger.LogError(ex, "All providers failed for chat completion.");
                return Results.Problem(
                    title: "All providers failed",
                    detail: ex.InnerException?.Message ?? ex.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in chat completions");
                return Results.Problem(
                    title: "Unexpected error",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("CreateChatCompletion");
    }

    private static RoutingDecision RerouteForStructuredOutput(
        RoutingDecision decision,
        ChatRequest request,
        IProviderRegistry registry,
        ILogger logger)
    {
        if (!request.ResponseSchema.HasValue) return decision;
        if (decision.IsProviderPinned) return decision;
        if (decision.Provider != AiProvider.Anthropic) return decision;

        AiProvider[] schemaCapable = [AiProvider.Google, AiProvider.OpenAi];
        AiProvider? replacement = schemaCapable.Cast<AiProvider?>().FirstOrDefault(p => registry.IsAvailable(p!.Value));
        if (replacement is null) return decision;

        logger.LogInformation(
            "Rerouting structured-output request from {From} to {To} (Anthropic OpenAI-compat does not support response_format json_schema).",
            decision.Provider, replacement);

        return decision with { Provider = replacement.Value };
    }

    private static List<AITool> BuildTools(IReadOnlyList<string> requiredSkills, MemorySkill memorySkill)
    {
        var tools = new List<AITool>();

        foreach (var skill in requiredSkills)
        {
            switch (skill)
            {
                case "code":
                    tools.AddRange(CodeSkill.GetTools());
                    break;
                case "search":
                    tools.AddRange(WebSearchSkill.GetTools());
                    break;
                case "memory":
                    tools.AddRange(memorySkill.GetTools());
                    break;
                case "time":
                    tools.AddRange(TimeSkill.GetTools());
                    break;
            }
        }

        return tools;
    }
}
