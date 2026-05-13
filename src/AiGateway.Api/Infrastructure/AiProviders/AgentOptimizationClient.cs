using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.AiProviders;

public class AgentOptimizationClient : DelegatingChatClient
{
    private readonly string _systemPromptFragment;
    private readonly string? _callerSystemInstruction;

    public AgentOptimizationClient(
        IChatClient innerClient,
        string systemPromptFragment,
        string? callerSystemInstruction = null) : base(innerClient)
    {
        _systemPromptFragment = systemPromptFragment;
        _callerSystemInstruction = callerSystemInstruction;
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
        var parts = new List<string> { _systemPromptFragment };
        if (!string.IsNullOrWhiteSpace(_callerSystemInstruction))
            parts.Add(_callerSystemInstruction);

        var existing = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (existing is not null)
        {
            messages.Remove(existing);
            parts.Add(existing.Text);
        }

        messages.Insert(0, new ChatMessage(ChatRole.System, string.Join("\n\n", parts)));
    }
}
