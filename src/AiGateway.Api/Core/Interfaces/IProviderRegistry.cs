using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Core.Interfaces;

public interface IProviderRegistry
{
    IReadOnlyList<ProviderDescriptor> GetAvailable();
    bool IsAvailable(AiProvider provider);
    IChatClient CreateClient(AiProvider provider, ModelComplexity complexity);
    IChatClient CreateResilientClient(AiProvider provider, ModelComplexity complexity, ILogger logger);
    ProviderDescriptor? GetNext(AiProvider current);
}
