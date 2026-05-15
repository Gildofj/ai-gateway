using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Features.Agents;
using AiGateway.Api.Features.AppContext;
using AiGateway.Api.Features.Chat;
using AiGateway.Api.Features.Embeddings;
using AiGateway.Api.Features.Memory;
using AiGateway.Api.Features.PromptEnhancement;
using AiGateway.Api.Features.Sessions;
using AiGateway.Api.Infrastructure.AiProviders;
using AiGateway.Api.Infrastructure.Configuration;
using AiGateway.Api.Infrastructure.Cost;
using AiGateway.Api.Infrastructure.Persistence;
using AiGateway.Api.Skills;
using Microsoft.Extensions.AI;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Cloud Run / generic container runtimes inject PORT. Bind Kestrel to it
// so the same image runs locally (PORT unset → launchSettings) and in the cloud.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHttpContextAccessor();

// Persistence & Context
builder.Services.AddFirestore(builder.Configuration);
builder.Services.AddScoped<IAppContext, HttpAppContextAccessor>();
builder.Services.AddScoped<IMemoryStore, FirestoreMemoryStore>();
builder.Services.AddScoped<ICustomAgentStore, FirestoreCustomAgentStore>();
builder.Services.AddScoped<ISessionStore, FirestoreSessionStore>();

// Providers & AI Services
builder.Services.AddHttpClient(GoogleClientFactory.HttpClientName, c =>
    c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"));

builder.Services.AddSingleton<IProviderClientFactory, OpenAiClientFactory>();
builder.Services.AddSingleton<IProviderClientFactory, AnthropicClientFactory>();
builder.Services.AddSingleton<IProviderClientFactory, GoogleClientFactory>();

builder.Services.AddSingleton<IProviderRegistry, ProviderRegistry>();
builder.Services.AddSingleton<ITaskAnalyzer, TaskAnalyzer>();

// Embeddings
builder.Services.AddSingleton<EmbeddingCache>();
builder.Services.AddSingleton<IEmbeddingProviderFactory, OpenAiEmbeddingFactory>();

// Domain Agents
builder.Services.AddSingleton<IDomainAgent, CodingAgent>();
builder.Services.AddSingleton<IDomainAgent, ResearchAgent>();
builder.Services.AddSingleton<IDomainAgent, WritingAgent>();
builder.Services.AddSingleton<IDomainAgent, AnalysisAgent>();
builder.Services.AddSingleton<IDomainAgent, MathAgent>();
builder.Services.AddSingleton<IDomainAgent, TranslationAgent>();
builder.Services.AddSingleton<IDomainAgent, ConversationAgent>();
builder.Services.AddSingleton<IDomainAgent, GeneralAgent>();
builder.Services.AddScoped<AgentSelector>();

builder.Services.AddSingleton<IPromptEnhancer, PromptEnhancer>();
builder.Services.AddSingleton<ICostTracker, CostTracker>();
builder.Services.AddScoped<MemorySkill>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 1. AppContext Middleware (Extract X-App-Id)
app.UseMiddleware<AppContextMiddleware>();

// 2. X-API-Key guard. When GATEWAY_API_KEY is set, every /api/* call must carry a
// matching header. Without this the public Cloud Run URL is open season for the
// free tier — see docs/deployment.md §7.
var gatewayApiKey = builder.Configuration["GATEWAY_API_KEY"];

if (!string.IsNullOrWhiteSpace(gatewayApiKey))
{
    var expectedKey = Encoding.UTF8.GetBytes(gatewayApiKey);

    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next();
            return;
        }

        var provided = context.Request.Headers["X-API-Key"].ToString();
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var ok = providedBytes.Length == expectedKey.Length
                 && CryptographicOperations.FixedTimeEquals(providedBytes, expectedKey);

        if (!ok)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing X-API-Key" });
            return;
        }

        await next();
    });
}
else if (!app.Environment.IsDevelopment())
{
    app.Logger.LogWarning("GATEWAY_API_KEY is not set — /api/* is unauthenticated. Anyone with the URL can drain your free tier.");
}

// Features Endpoints
app.MapChatEndpoints();
app.MapMemoryEndpoints();
app.MapEmbeddingEndpoints();
app.MapAgentEndpoints();
app.MapSessionEndpoints();

app.Run();
