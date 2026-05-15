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
            IModelRouter router,
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

            // Update AppId from request if provided
            if (!string.IsNullOrEmpty(request.AppId))
            {
                httpContext.Items["AppId"] = request.AppId;
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

                // 1. Analyze task — reuse session decision or skip if explicit
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

                // 2. Select domain agent → routing decision
                var (decision, agentScope) = await agentSelector.SelectAsync(analysis, request.AgentId ?? session?.AgentId, request.Provider ?? session?.Provider);

                // 3. Enhance prompt with domain-specific hint
                var actualPrompt = request.Prompt;
                string? enhancedPrompt = null;

                if (request.EnablePromptEnhancement)
                {
                    var hint = agentSelector.GetEnhancementHint(analysis.Domain);
                    actualPrompt = await enhancer.EnhanceAsync(request.Prompt, hint, cancellationToken);
                    enhancedPrompt = actualPrompt;
                }

                // 4. Get resilient client and model name
                var chatClient = router.GetClient(decision);
                var modelName = router.GetModelName(decision);

                // 5. Inject domain agent system prompt (+ optional caller system instruction)
                chatClient = new AgentOptimizationClient(chatClient, decision.SystemPromptFragment, request.SystemInstruction);

                // 6. Build tools from the skills required by this domain agent
                var chatOptions = new ChatOptions { ModelId = modelName };

                if (request.UseSkills && decision.RequiredSkills.Count > 0)
                {
                    chatOptions.Tools = BuildTools(decision.RequiredSkills, memorySkill);
                }

                if (string.Equals(request.ResponseMimeType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    chatOptions.ResponseFormat = request.ResponseSchema.HasValue
                        ? ChatResponseFormat.ForJsonSchema(request.ResponseSchema.Value)
                        : ChatResponseFormat.Json;
                }

                // 7. Prepare messages (including session history)
                var messages = new List<ChatMessage>();
                if (session != null)
                {
                    foreach (var turn in session.Turns)
                    {
                        messages.Add(new ChatMessage(new ChatRole(turn.Role), turn.Content));
                    }
                }
                messages.Add(new ChatMessage(ChatRole.User, actualPrompt));

                // 8. Execute
                var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);

                // 9. Update session
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
                            Provider = decision.Provider,
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

                    // Keep only last 6 turns (matching AgentOptimizationClient context-pruning suggestion)
                    if (session.Turns.Count > 6)
                    {
                        session.Turns.RemoveRange(0, session.Turns.Count - 6);
                    }
                    
                    session = session with { TurnCount = session.Turns.Count };
                    await sessionStore.UpsertAsync(session);
                }

                // 10. Track usage and cost
                var usage = response.Usage is not null
                    ? new TokenUsage(
                        response.Usage.InputTokenCount ?? 0,
                        response.Usage.OutputTokenCount ?? 0,
                        response.Usage.TotalTokenCount ?? 0)
                    : null;

                var estimatedCost = usage is not null
                    ? costTracker.EstimateCost(modelName, usage.InputTokens, usage.OutputTokens)
                    : (decimal?)null;

                return Results.Ok(new GatewayResponse
                {
                    Completion = response.Text ?? string.Empty,
                    ModelUsed = modelName,
                    ProviderUsed = decision.Provider,
                    Domain = analysis.Domain,
                    EnhancedPrompt = enhancedPrompt,
                    Usage = usage,
                    EstimatedCost = estimatedCost,
                    SessionHit = sessionHit,
                    AppId = appContext.AppId,
                    AgentScope = agentScope
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in chat completions");
                return Results.Problem("An unexpected error occurred while processing your request.", statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("CreateChatCompletion");
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
