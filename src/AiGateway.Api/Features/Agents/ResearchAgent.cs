using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class ResearchAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Research;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.Google, AiProvider.OpenAi];
    public string SystemPromptFragment => "You are a research analyst. Use available tools to find up-to-date information. Cite sources when available. Structure findings clearly.";
    public IReadOnlyList<string> RequiredSkills => ["search"];
    public string EnhancementHint => "Specify time range, geographic scope, required depth, and preferred output structure.";
}
