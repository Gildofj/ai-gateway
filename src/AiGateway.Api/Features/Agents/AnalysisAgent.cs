using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class AnalysisAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Analysis;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.Anthropic, AiProvider.Google];
    public string SystemPromptFragment => "You are a data and systems analyst. Break down problems systematically. Identify patterns, anomalies, and root causes. Support conclusions with evidence.";
    public IReadOnlyList<string> RequiredSkills => ["code"];
    public string EnhancementHint => "Specify what to analyze, the expected output (summary, table, chart description), and what decisions the analysis should support.";
}
