using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Core.Interfaces;

public interface IDomainAgent
{
    TaskDomain Domain { get; }
    IReadOnlyList<AiProvider> PreferredProviders { get; }
    string SystemPromptFragment { get; }
    IReadOnlyList<string> RequiredSkills { get; }
    string EnhancementHint { get; }
}
