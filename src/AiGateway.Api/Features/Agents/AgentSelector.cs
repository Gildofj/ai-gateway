using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class AgentSelector
{
    private readonly IReadOnlyDictionary<TaskDomain, IDomainAgent> _agents;
    private readonly IProviderRegistry _registry;

    public AgentSelector(IEnumerable<IDomainAgent> agents, IProviderRegistry registry)
    {
        _agents = agents.ToDictionary(a => a.Domain);
        _registry = registry;
    }

    public RoutingDecision Select(TaskAnalysis analysis, AiProvider? explicitProvider = null)
    {
        var agent = _agents.GetValueOrDefault(analysis.Domain) ?? _agents[TaskDomain.General];

        var provider = explicitProvider ?? ResolveProvider(agent);

        return new RoutingDecision(analysis, provider, agent.SystemPromptFragment, agent.RequiredSkills);
    }

    public string GetEnhancementHint(TaskDomain domain)
    {
        return _agents.TryGetValue(domain, out var agent) ? agent.EnhancementHint : string.Empty;
    }

    private AiProvider ResolveProvider(IDomainAgent agent)
    {
        var preferred = agent.PreferredProviders.FirstOrDefault(_registry.IsAvailable);
        if (preferred != default)
            return preferred;

        var any = _registry.GetAvailable().FirstOrDefault();
        return any?.Provider ?? AiProvider.OpenAi;
    }
}
