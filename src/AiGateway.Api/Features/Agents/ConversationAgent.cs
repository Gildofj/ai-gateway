using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class ConversationAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Conversation;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.Google, AiProvider.OpenAi, AiProvider.Anthropic];
    public string SystemPromptFragment => "Be friendly, helpful, and concise.";
    public IReadOnlyList<string> RequiredSkills => [];
    public string EnhancementHint => "";
}
