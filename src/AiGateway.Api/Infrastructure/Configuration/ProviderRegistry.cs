using System.ClientModel;
using System.Collections.Concurrent;
using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Infrastructure.AiProviders;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.Configuration;

public class ProviderRegistry : IProviderRegistry
{
    private static readonly TimeSpan AuthFailureCooldown = TimeSpan.FromMinutes(5);

    private readonly List<ProviderDescriptor> _available;
    private readonly Dictionary<AiProvider, IProviderClientFactory> _factories;
    private readonly ConcurrentDictionary<AiProvider, DateTimeOffset> _unhealthyUntil = new();
    private readonly ILogger<ProviderRegistry> _logger;

    public ProviderRegistry(
        IConfiguration configuration,
        ILogger<ProviderRegistry> logger,
        IEnumerable<IProviderClientFactory> factories)
    {
        _logger = logger;
        _available = DiscoverProviders(configuration, logger);
        _factories = factories.ToDictionary(f => f.Provider);
    }

    public IReadOnlyList<ProviderDescriptor> GetAvailable() =>
        _available.Where(d => IsHealthy(d.Provider)).ToList();

    public bool IsAvailable(AiProvider provider) =>
        IsConfigured(provider) && IsHealthy(provider);

    public bool IsConfigured(AiProvider provider) =>
        _available.Any(d => d.Provider == provider);

    public IChatClient CreateClient(AiProvider provider, ModelComplexity complexity)
    {
        var descriptor = _available.FirstOrDefault(d => d.Provider == provider)
            ?? throw new InvalidOperationException($"Provider {provider} is not configured on this gateway.");

        if (!_factories.TryGetValue(descriptor.Provider, out var factory))
        {
            throw new InvalidOperationException(
                $"No IProviderClientFactory registered for provider {descriptor.Provider}.");
        }

        var client = factory.Create(descriptor, complexity);
        return client.AddProviderOptimizations(descriptor.Provider.ToString().ToLower());
    }

    public ProviderDescriptor? GetNext(AiProvider current) =>
        _available.FirstOrDefault(d => d.Provider != current && IsHealthy(d.Provider));

    public void MarkUnhealthy(AiProvider provider, TimeSpan cooldown, string reason)
    {
        var until = DateTimeOffset.UtcNow.Add(cooldown);
        _unhealthyUntil[provider] = until;
        _logger.LogWarning(
            "Provider {Provider} marked unhealthy until {Until:O}. Reason: {Reason}",
            provider, until, reason);
    }

    public async Task<T> ExecuteAsync<T>(
        AiProvider preferredProvider,
        ModelComplexity complexity,
        Func<ProviderClientContext, Task<T>> action,
        bool allowFallback = true,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(preferredProvider))
            throw new ProviderNotConfiguredException(preferredProvider);

        if (!allowFallback)
        {
            try
            {
                return await InvokeAsync(preferredProvider, complexity, action);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                if (IsAuthFailure(ex))
                    MarkUnhealthy(preferredProvider, AuthFailureCooldown, ex.Message);
                throw;
            }
        }

        var order = BuildFallbackOrder(preferredProvider);
        Exception? lastError = null;

        for (var i = 0; i < order.Count; i++)
        {
            var provider = order[i];

            if (i > 0)
                _logger.LogWarning("Falling back to provider {Provider}.", provider);

            try
            {
                return await InvokeAsync(provider, complexity, action);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                lastError = ex;
                _logger.LogWarning(ex, "Provider {Provider} failed.", provider);
                if (IsAuthFailure(ex))
                    MarkUnhealthy(provider, AuthFailureCooldown, ex.Message);
            }
        }

        throw new InvalidOperationException(
            $"All AI providers failed. Last error: {lastError?.Message ?? "unknown"}", lastError);
    }

    private async Task<T> InvokeAsync<T>(
        AiProvider provider,
        ModelComplexity complexity,
        Func<ProviderClientContext, Task<T>> action)
    {
        var descriptor = _available.First(d => d.Provider == provider);
        var client = CreateClient(provider, complexity);
        var modelName = complexity == ModelComplexity.High
            ? descriptor.CapableModel
            : descriptor.FastModel;
        return await action(new ProviderClientContext(client, provider, modelName));
    }

    private List<AiProvider> BuildFallbackOrder(AiProvider preferred)
    {
        var ordered = new List<AiProvider>();
        if (IsHealthy(preferred))
            ordered.Add(preferred);

        foreach (var descriptor in _available)
        {
            if (descriptor.Provider == preferred) continue;
            if (!IsHealthy(descriptor.Provider)) continue;
            ordered.Add(descriptor.Provider);
        }

        if (ordered.Count == 0)
            ordered.Add(preferred);

        return ordered;
    }

    private bool IsHealthy(AiProvider provider)
    {
        if (!_unhealthyUntil.TryGetValue(provider, out var until))
            return true;

        if (DateTimeOffset.UtcNow < until)
            return false;

        _unhealthyUntil.TryRemove(provider, out _);
        return true;
    }

    private static bool IsAuthFailure(Exception ex)
    {
        if (ex is ClientResultException cre && (cre.Status == 401 || cre.Status == 403))
            return true;

        var message = ex.Message ?? string.Empty;
        return message.Contains("401", StringComparison.Ordinal)
            || message.Contains("403", StringComparison.Ordinal)
            || message.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase);
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
