using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class CodingAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Coding;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.Anthropic, AiProvider.OpenAi];
    public string SystemPromptFragment => "You are a senior software engineer. Write clean, idiomatic, well-structured code. Be extremely concise in prose explanations — let the code speak.";
    public IReadOnlyList<string> RequiredSkills => ["code", "memory"];
    public string EnhancementHint => "Clarify the programming language, framework version, input/output contract, and any relevant constraints.";
}
