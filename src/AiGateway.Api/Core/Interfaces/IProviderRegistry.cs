using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Core.Interfaces;

public interface IProviderRegistry
{
    IReadOnlyList<ProviderDescriptor> GetAvailable();
    bool IsAvailable(AiProvider provider);
    bool IsConfigured(AiProvider provider);
    IChatClient CreateClient(AiProvider provider, ModelComplexity complexity);
    ProviderDescriptor? GetNext(AiProvider current);
    void MarkUnhealthy(AiProvider provider, TimeSpan cooldown, string reason);

    Task<T> ExecuteAsync<T>(
        AiProvider preferredProvider,
        ModelComplexity complexity,
        Func<ProviderClientContext, Task<T>> action,
        bool allowFallback = true,
        CancellationToken cancellationToken = default);
}

public record ProviderClientContext(IChatClient Client, AiProvider Provider, string ModelName);
