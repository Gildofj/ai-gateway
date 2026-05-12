using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class MathAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Math;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.OpenAi, AiProvider.Google];
    public string SystemPromptFragment => "You are a mathematics expert. Show step-by-step reasoning. Verify results. Use precise notation.";
    public IReadOnlyList<string> RequiredSkills => [];
    public string EnhancementHint => "Clarify the domain (algebra, calculus, statistics), required precision, and whether symbolic or numerical results are expected.";
}
