using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Infrastructure.AiProviders;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.Configuration;

public class ProviderRegistry : IProviderRegistry
{
    private readonly List<ProviderDescriptor> _available;
    private readonly Dictionary<AiProvider, IProviderClientFactory> _factories;

    public ProviderRegistry(
        IConfiguration configuration,
        ILogger<ProviderRegistry> logger,
        IEnumerable<IProviderClientFactory> factories)
    {
        _available = DiscoverProviders(configuration, logger);
        _factories = factories.ToDictionary(f => f.Provider);
    }

    public IReadOnlyList<ProviderDescriptor> GetAvailable() => _available;

    public bool IsAvailable(AiProvider provider) =>
        _available.Any(d => d.Provider == provider);

    public IChatClient CreateClient(AiProvider provider, ModelComplexity complexity)
    {
        var descriptor = _available.FirstOrDefault(d => d.Provider == provider)
            ?? _available.First();

        if (!_factories.TryGetValue(descriptor.Provider, out var factory))
        {
            throw new InvalidOperationException(
                $"No IProviderClientFactory registered for provider {descriptor.Provider}.");
        }

        var client = factory.Create(descriptor, complexity);
        return client.AddProviderOptimizations(descriptor.Provider.ToString().ToLower());
    }

    public IChatClient CreateResilientClient(AiProvider provider, ModelComplexity complexity, ILogger logger)
    {
        var primary = CreateClient(provider, complexity);
        return new FallbackChatClient(primary, this, provider, complexity, logger);
    }

    public ProviderDescriptor? GetNext(AiProvider current)
    {
        var others = _available.Where(d => d.Provider != current).ToList();
        return others.FirstOrDefault();
    }

    private static List<ProviderDescriptor> DiscoverProviders(IConfiguration config, ILogger<ProviderRegistry> logger)
    {
        var providers = new List<ProviderDescriptor>();

        TryAdd(providers, config, AiProvider.OpenAi,
            keyPath: "AI:OpenAi:ApiKey",
            envVar: "OPENAI_API_KEY",
            defaultFast: "gpt-5.4-mini",
            defaultCapable: "gpt-5.5-thinking",
            endpoint: null);

        TryAdd(providers, config, AiProvider.Google,
            keyPath: "AI:Google:ApiKey",
            envVar: "GOOGLE_API_KEY",
            defaultFast: "gemini-3.1-flash-lite",
            defaultCapable: "gemini-3.1-pro",
            endpoint: null);

        TryAdd(providers, config, AiProvider.Anthropic,
            keyPath: "AI:Anthropic:ApiKey",
            envVar: "ANTHROPIC_API_KEY",
            defaultFast: "claude-haiku-4-5",
            defaultCapable: "claude-opus-4-7",
            endpoint: new Uri("https://api.anthropic.com/v1/messages/openai"));

        if (providers.Count == 0)
        {
            logger.LogError("No AI providers were configured! Check environment variables: OPENAI_API_KEY, GOOGLE_API_KEY, ANTHROPIC_API_KEY.");
        }
        else
        {
            logger.LogInformation("Successfully configured {Count} providers: {Providers}",
                providers.Count, string.Join(", ", providers.Select(p => p.Provider)));
        }

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
        var apiKey = config[keyPath];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = config[envVar];
        }

        if (string.IsNullOrWhiteSpace(apiKey)) return;
        if (apiKey.Contains("placeholder", StringComparison.OrdinalIgnoreCase)) return;

        var fastModel = config[$"{keyPath.Replace(":ApiKey", ":FastModel")}"] ?? defaultFast;
        var capableModel = config[$"{keyPath.Replace(":ApiKey", ":CapableModel")}"] ?? defaultCapable;

        providers.Add(new ProviderDescriptor(provider, apiKey, fastModel, capableModel, endpoint));
    }
}
