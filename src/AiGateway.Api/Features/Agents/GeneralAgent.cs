using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class GeneralAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.General;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.OpenAi, AiProvider.Google];
    public string SystemPromptFragment => "Be helpful and concise.";
    public IReadOnlyList<string> RequiredSkills => [];
    public string EnhancementHint => "Be explicit about the desired output format and level of detail.";
}
