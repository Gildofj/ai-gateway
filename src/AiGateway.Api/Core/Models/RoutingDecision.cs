namespace AiGateway.Api.Core.Models;

public record RoutingDecision(
    TaskAnalysis Analysis,
    AiProvider Provider,
    string SystemPromptFragment,
    IReadOnlyList<string> RequiredSkills);
