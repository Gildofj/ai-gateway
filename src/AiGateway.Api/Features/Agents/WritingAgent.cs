using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class WritingAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Writing;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.Anthropic, AiProvider.OpenAi];
    public string SystemPromptFragment => "You are a professional writer. Adapt tone and style to the audience. Prioritize clarity, flow, and precision.";
    public IReadOnlyList<string> RequiredSkills => ["memory"];
    public string EnhancementHint => "Specify target audience, desired tone (formal/casual), length, and format (email, essay, post, etc.).";
}
