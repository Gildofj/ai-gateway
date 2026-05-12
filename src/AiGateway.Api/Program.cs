using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Features.Agents;
using AiGateway.Api.Features.PromptEnhancement;
using AiGateway.Api.Features.Routing;
using AiGateway.Api.Infrastructure.AiProviders;
using AiGateway.Api.Infrastructure.Configuration;
using AiGateway.Api.Infrastructure.Cost;
using AiGateway.Api.Skills;
using Microsoft.Extensions.AI;
using GatewayResponse = AiGateway.Api.Core.Models.ChatResponse;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddSingleton<IProviderRegistry, ProviderRegistry>();
builder.Services.AddSingleton<ITaskAnalyzer, TaskAnalyzer>();

builder.Services.AddSingleton<IDomainAgent, CodingAgent>();
builder.Services.AddSingleton<IDomainAgent, ResearchAgent>();
builder.Services.AddSingleton<IDomainAgent, WritingAgent>();
builder.Services.AddSingleton<IDomainAgent, AnalysisAgent>();
builder.Services.AddSingleton<IDomainAgent, MathAgent>();
builder.Services.AddSingleton<IDomainAgent, TranslationAgent>();
builder.Services.AddSingleton<IDomainAgent, ConversationAgent>();
builder.Services.AddSingleton<IDomainAgent, GeneralAgent>();
builder.Services.AddSingleton<AgentSelector>();

builder.Services.AddSingleton<IModelRouter, ModelRouter>();
builder.Services.AddSingleton<IPromptEnhancer, PromptEnhancer>();
builder.Services.AddSingleton<ICostTracker, CostTracker>();
builder.Services.AddScoped<MemorySkill>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

app.MapPost("/api/v1/chat/completions", async (
    ChatRequest request,
    ITaskAnalyzer analyzer,
    AgentSelector agentSelector,
    IModelRouter router,
    IPromptEnhancer enhancer,
    ICostTracker costTracker,
    MemorySkill memorySkill,
    CancellationToken cancellationToken) =>
{
    // 1. Analyze task — skip AI call if both domain and complexity are explicit
    var analysis = (request.Domain.HasValue && request.Complexity.HasValue)
        ? new TaskAnalysis(request.Domain.Value, request.Complexity.Value)
        : await analyzer.AnalyzeAsync(request.Prompt, cancellationToken);

    // 2. Select domain agent → routing decision
    var decision = agentSelector.Select(analysis, request.Provider);

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

    // 5. Inject domain agent system prompt
    chatClient = new AgentOptimizationClient(chatClient, decision.SystemPromptFragment);

    // 6. Build tools from the skills required by this domain agent
    var chatOptions = new ChatOptions { ModelId = modelName };

    if (request.UseSkills && decision.RequiredSkills.Count > 0)
    {
        chatOptions.Tools = BuildTools(decision.RequiredSkills, memorySkill);
    }

    // 7. Execute
    var response = await chatClient.GetResponseAsync(actualPrompt, chatOptions, cancellationToken);

    // 8. Track usage and cost
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
        EstimatedCost = estimatedCost
    });
})
.WithName("CreateChatCompletion");

app.Run();

static List<AITool> BuildTools(IReadOnlyList<string> requiredSkills, MemorySkill memorySkill)
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
