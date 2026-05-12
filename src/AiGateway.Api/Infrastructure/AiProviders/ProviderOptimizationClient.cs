using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.AiProviders;

public class ProviderOptimizationClient : DelegatingChatClient
{
    private readonly string _providerName;

    public ProviderOptimizationClient(IChatClient innerClient, string providerName) : base(innerClient)
    {
        _providerName = providerName.ToLower();
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        ApplyOptimizations(messageList, options);
        return await base.GetResponseAsync(messageList, options, cancellationToken);
    }

    private void ApplyOptimizations(List<ChatMessage> messages, ChatOptions? options)
    {
        switch (_providerName)
        {
            case "anthropic":
                // Claude optimization: Ensure system prompts are handled correctly 
                // and add a hint for conciseness if not specified.
                if (!messages.Any(m => m.Role == ChatRole.System))
                {
                    messages.Insert(0, new ChatMessage(ChatRole.System, "Respond concisely and use markdown for code."));
                }
                break;

            case "google":
                // Gemini optimization: Gemini 1.5 Pro works best with very explicit instructions 
                // about output format.
                if (options?.Tools?.Any() == true)
                {
                    messages.Insert(0, new ChatMessage(ChatRole.System, "You have access to tools. Use them only when necessary."));
                }
                break;

            case "openai":
                // GPT-4o optimization: Enable specific features if needed
                break;
        }
    }
}

public static class ChatClientExtensions
{
    public static IChatClient AddProviderOptimizations(this IChatClient client, string providerName)
    {
        return new ProviderOptimizationClient(client, providerName);
    }
}
