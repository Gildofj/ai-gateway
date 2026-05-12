using Microsoft.Extensions.AI;

namespace AiGateway.Api.Tests.Helpers;

/// <summary>
/// Deterministic IChatClient for unit tests. Returns a pre-configured response
/// and optionally throws on demand. Tracks how many times it was called.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly Func<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> _handler;

    public int CallCount { get; private set; }

    internal FakeChatClient(string responseText = "fake response")
    {
        _handler = (_, _, _) => Task.FromResult(
            new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));
    }

    internal FakeChatClient(Exception toThrow)
    {
        _handler = (_, _, _) => throw toThrow;
    }

    internal FakeChatClient(Func<IEnumerable<ChatMessage>, ChatResponse> handler)
    {
        _handler = (msgs, _, _) => Task.FromResult(handler(msgs));
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        cancellationToken.ThrowIfCancellationRequested();
        return _handler(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<StreamingChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ChatClientMetadata Metadata => new(null);
    public TService? GetService<TService>(object? key = null) where TService : class => null;
    public void Dispose() { }
}
