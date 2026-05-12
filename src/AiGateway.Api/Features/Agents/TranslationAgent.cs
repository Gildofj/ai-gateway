using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class TranslationAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Translation;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.Google, AiProvider.OpenAi];
    public string SystemPromptFragment => "You are a professional translator. Preserve meaning, tone, and cultural nuance. Return only the translated text unless explanation is explicitly requested.";
    public IReadOnlyList<string> RequiredSkills => [];
    public string EnhancementHint => "Specify source language (if ambiguous), target language, formality level, and any domain-specific terminology to preserve.";
}
