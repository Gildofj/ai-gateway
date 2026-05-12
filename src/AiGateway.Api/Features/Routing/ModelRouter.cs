using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using AiGateway.Api.Infrastructure.AiProviders;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.Routing;

public class ModelRouter : IModelRouter
{
    private readonly IProviderRegistry _registry;
    private readonly ILogger<FallbackChatClient> _fallbackLogger;

    public ModelRouter(IProviderRegistry registry, ILogger<FallbackChatClient> fallbackLogger)
    {
        _registry = registry;
        _fallbackLogger = fallbackLogger;
    }

    public IChatClient GetClient(RoutingDecision decision)
    {
        var primary = _registry.CreateClient(decision.Provider, decision.Analysis.Complexity);
        return new FallbackChatClient(primary, _registry, decision.Provider, decision.Analysis.Complexity, _fallbackLogger);
    }

    public string GetModelName(RoutingDecision decision)
    {
        var descriptor = _registry.GetAvailable().FirstOrDefault(d => d.Provider == decision.Provider);
        if (descriptor is null)
            return "unknown";

        return decision.Analysis.Complexity == ModelComplexity.High
            ? descriptor.CapableModel
            : descriptor.FastModel;
    }
}
