using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Infrastructure.AiProviders;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace AiGateway.Api.Infrastructure.Configuration;

public class ProviderRegistry : IProviderRegistry
{
    private readonly List<ProviderDescriptor> _available;

    public ProviderRegistry(IConfiguration configuration)
    {
        _available = DiscoverProviders(configuration);
    }

    public IReadOnlyList<ProviderDescriptor> GetAvailable() => _available;

    public bool IsAvailable(AiProvider provider) =>
        _available.Any(d => d.Provider == provider);

    public IChatClient CreateClient(AiProvider provider, ModelComplexity complexity)
    {
        var descriptor = _available.FirstOrDefault(d => d.Provider == provider)
            ?? _available.First();

        var modelId = complexity == ModelComplexity.High ? descriptor.CapableModel : descriptor.FastModel;
        var credential = new ApiKeyCredential(descriptor.ApiKey);

        var options = descriptor.Endpoint is not null
            ? new OpenAIClientOptions { Endpoint = descriptor.Endpoint }
            : null;

        var client = options is not null
            ? new OpenAI.Chat.ChatClient(modelId, credential, options).AsIChatClient()
            : new OpenAI.Chat.ChatClient(modelId, credential).AsIChatClient();

        return client.AddProviderOptimizations(provider.ToString().ToLower());
    }

    public ProviderDescriptor? GetNext(AiProvider current)
    {
        var others = _available.Where(d => d.Provider != current).ToList();
        return others.FirstOrDefault();
    }

    private static List<ProviderDescriptor> DiscoverProviders(IConfiguration config)
    {
        var providers = new List<ProviderDescriptor>();

        TryAdd(providers, config, AiProvider.OpenAi,
            keyPath: "AI:OpenAi:ApiKey",
            envVar: "OPENAI_API_KEY",
            defaultFast: "gpt-5.4-mini",
            defaultCapable: "gpt-5.5",
            endpoint: null);

        TryAdd(providers, config, AiProvider.Google,
            keyPath: "AI:Google:ApiKey",
            envVar: "GOOGLE_API_KEY",
            defaultFast: "gemini-3.0-flash-preview",
            defaultCapable: "gemini-3.1-pro-preview",
            endpoint: new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"));

        TryAdd(providers, config, AiProvider.Anthropic,
            keyPath: "AI:Anthropic:ApiKey",
            envVar: "ANTHROPIC_API_KEY",
            defaultFast: "claude-4-haiku",
            defaultCapable: "claude-4-sonnet",
            endpoint: new Uri("https://api.anthropic.com/v1/messages/openai/"));

        return providers;
    }

    private static void TryAdd(
        List<ProviderDescriptor> providers,
        IConfiguration config,
        AiProvider provider,
        string keyPath,
        string envVar,
        string defaultFast,
        string defaultCapable,
        Uri? endpoint)
    {
        var apiKey = config[keyPath] ?? Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
            return;

        var fastModel = config[$"{keyPath.Replace(":ApiKey", ":FastModel")}"] ?? defaultFast;
        var capableModel = config[$"{keyPath.Replace(":ApiKey", ":CapableModel")}"] ?? defaultCapable;

        providers.Add(new ProviderDescriptor(provider, apiKey, fastModel, capableModel, endpoint));
    }
}
