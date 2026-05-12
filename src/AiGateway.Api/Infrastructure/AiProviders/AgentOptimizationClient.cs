using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.AiProviders;

public class AgentOptimizationClient : DelegatingChatClient
{
    private readonly string _systemPromptFragment;

    public AgentOptimizationClient(IChatClient innerClient, string systemPromptFragment) : base(innerClient)
    {
        _systemPromptFragment = systemPromptFragment;
    }

    public override async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        PruneContext(messageList);
        InjectSystemPrompt(messageList);
        return await base.GetResponseAsync(messageList, options, cancellationToken);
    }

    private static void PruneContext(List<ChatMessage> messages)
    {
        if (messages.Count <= 10)
            return;

        var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();
        var nonSystemMessages = messages.Where(m => m.Role != ChatRole.System).ToList();

        if (nonSystemMessages.Count <= 8)
            return;

        var kept = nonSystemMessages.Skip(nonSystemMessages.Count - 6).ToList();
        messages.Clear();
        messages.AddRange(systemMessages);
        messages.AddRange(kept);
    }

    private void InjectSystemPrompt(List<ChatMessage> messages)
    {
        var existing = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (existing is not null)
        {
            messages.Remove(existing);
            messages.Insert(0, new ChatMessage(ChatRole.System, $"{_systemPromptFragment}\n\n{existing.Text}"));
        }
        else
        {
            messages.Insert(0, new ChatMessage(ChatRole.System, _systemPromptFragment));
        }
    }
}
