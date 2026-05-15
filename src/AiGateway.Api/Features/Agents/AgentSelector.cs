using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class AgentSelector
{
    private readonly IReadOnlyDictionary<TaskDomain, IDomainAgent> _agents;
    private readonly IProviderRegistry _registry;
    private readonly ICustomAgentStore _customAgentStore;

    public AgentSelector(IEnumerable<IDomainAgent> agents, IProviderRegistry registry, ICustomAgentStore customAgentStore)
    {
        _agents = agents.ToDictionary(a => a.Domain);
        _registry = registry;
        _customAgentStore = customAgentStore;
    }

    public async Task<(RoutingDecision decision, string scope)> SelectAsync(TaskAnalysis analysis, string? agentId = null, AiProvider? explicitProvider = null)
    {
        IDomainAgent? agent = null;
        string scope = "built-in";

        if (!string.IsNullOrEmpty(agentId))
        {
            // 1. Try custom agent (app-scoped or global)
            var custom = await _customAgentStore.GetAsync(agentId);
            if (custom != null)
            {
                agent = new CustomAgentAdapter(custom);
                scope = custom.Scope;
            }
            else
            {
                // 2. Try built-in by ID (lowercase name)
                agent = _agents.Values.FirstOrDefault(a => a.GetType().Name.Replace("Agent", "").Equals(agentId, StringComparison.OrdinalIgnoreCase));
            }
        }

        // 3. Fallback to domain-based selection
        if (agent == null)
        {
            agent = _agents.GetValueOrDefault(analysis.Domain) ?? _agents[TaskDomain.General];
            scope = "built-in";
        }

        var provider = explicitProvider ?? ResolveProvider(agent);

        return (new RoutingDecision(analysis, provider, agent.SystemPromptFragment, agent.RequiredSkills), scope);
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
