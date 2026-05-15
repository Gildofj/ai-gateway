using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class CustomAgentAdapter : IDomainAgent
{
    private readonly CustomAgent _agent;

    public CustomAgentAdapter(CustomAgent agent)
    {
        _agent = agent;
    }

    public TaskDomain Domain => _agent.Domain;
    public IReadOnlyList<AiProvider> PreferredProviders => _agent.PreferredProviders;
    public string SystemPromptFragment => _agent.SystemPromptFragment;
    public IReadOnlyList<string> RequiredSkills => _agent.RequiredSkills;
    public string EnhancementHint => _agent.EnhancementHint ?? string.Empty;
}
