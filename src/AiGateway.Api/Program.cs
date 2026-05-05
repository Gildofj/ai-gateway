using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Features.PromptEnhancement;
using AiGateway.Api.Features.Routing;
using AiGateway.Api.Skills;
using AiGateway.Api.Infrastructure.AiProviders;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// --- AI CONFIGURATION ---
var openAiKey = builder.Configuration["AI:OpenAi:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "sk-placeholder";
var googleKey = builder.Configuration["AI:Google:ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "placeholder";
var anthropicKey = builder.Configuration["AI:Anthropic:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "placeholder";

// Helper to create and optimize clients
IChatClient CreateOpenAiClient(string modelId) => 
    new OpenAI.Chat.ChatClient(modelId, new ApiKeyCredential(openAiKey)).AsIChatClient();

// Gemini via OpenAI-compatible endpoint (Most agnostic way)
IChatClient CreateGoogleClient(string modelId) => 
    new OpenAI.Chat.ChatClient(modelId, new ApiKeyCredential(googleKey), new OpenAI.OpenAIClientOptions 
    { 
        Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") 
    }).AsIChatClient();

// Claude via a hypothetical or real proxy (or custom client if needed)
// For this demo, we'll use OpenAI client but configured for Claude-compatible proxies
IChatClient CreateAnthropicClient(string modelId) => 
    new OpenAI.Chat.ChatClient(modelId, new ApiKeyCredential(anthropicKey), new OpenAI.OpenAIClientOptions 
    { 
        Endpoint = new Uri("https://api.anthropic.com/v1/messages/openai/") // Assuming a proxy or compatible endpoint
    }).AsIChatClient();

// Register Keyed Services for the Router
// OpenAI
builder.Services.AddKeyedSingleton<IChatClient>("openai-fast", (sp, key) => 
    CreateOpenAiClient("gpt-4o-mini").AddProviderOptimizations("openai"));
builder.Services.AddKeyedSingleton<IChatClient>("openai-capable", (sp, key) => 
    CreateOpenAiClient("gpt-4o").AddProviderOptimizations("openai"));

// Google
builder.Services.AddKeyedSingleton<IChatClient>("google-fast", (sp, key) => 
    CreateGoogleClient("gemini-1.5-flash").AddProviderOptimizations("google"));
builder.Services.AddKeyedSingleton<IChatClient>("google-capable", (sp, key) => 
    CreateGoogleClient("gemini-1.5-pro").AddProviderOptimizations("google"));

// Anthropic
builder.Services.AddKeyedSingleton<IChatClient>("anthropic-fast", (sp, key) => 
    CreateAnthropicClient("claude-3-haiku-20240307").AddProviderOptimizations("anthropic"));
builder.Services.AddKeyedSingleton<IChatClient>("anthropic-capable", (sp, key) => 
    CreateAnthropicClient("claude-3-5-sonnet-20240620").AddProviderOptimizations("anthropic"));

// Special Key for Prompt Enhancement (usually fast & cheap)
builder.Services.AddKeyedSingleton<IChatClient>("fast-model", (sp, key) => CreateOpenAiClient("gpt-4o-mini"));

// Register Gateway Features
builder.Services.AddSingleton<IPromptEnhancer, PromptEnhancer>();
builder.Services.AddSingleton<IModelRouter, ModelRouter>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// --- CHAT ENDPOINT ---
app.MapPost("/api/v1/chat/completions", async (
    ChatRequest request, 
    IModelRouter router, 
    IPromptEnhancer enhancer,
    CancellationToken cancellationToken) =>
{
    var actualPrompt = request.Prompt;
    var enhancedPrompt = string.Empty;

    // 1. Prompt Enhancement
    if (request.EnablePromptEnhancement)
    {
        actualPrompt = await enhancer.EnhancePromptAsync(request.Prompt, cancellationToken);
        enhancedPrompt = actualPrompt;
    }

    // 2. Resolve Client and Model
    var chatClient = router.GetClient(request);
    var modelName = router.GetModelName(request);

    // 3. Setup Options and Skills
    var chatOptions = new ChatOptions
    {
        ModelId = modelName
    };
    
    if (request.UseSkills)
    {
        chatOptions.Tools = new List<AITool>();
        foreach(var tool in TimeSkill.GetTools()) { chatOptions.Tools.Add(tool); }
        foreach(var tool in WebSearchSkill.GetTools()) { chatOptions.Tools.Add(tool); }
    }

    // 4. Execute with selected model
    var response = await chatClient.GetResponseAsync(actualPrompt, chatOptions, cancellationToken);

    return Results.Ok(new AiGateway.Api.Core.Models.ChatResponse
    {
        Completion = response.Text ?? "",
        ModelUsed = modelName,
        EnhancedPrompt = request.EnablePromptEnhancement ? enhancedPrompt : null
    });
})
.WithName("CreateChatCompletion");

app.Run();
