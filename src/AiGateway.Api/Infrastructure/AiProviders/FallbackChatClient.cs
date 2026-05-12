using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.AiProviders;

public class FallbackChatClient : DelegatingChatClient
{
    private readonly IProviderRegistry _registry;
    private readonly AiProvider _currentProvider;
    private readonly ModelComplexity _complexity;
    private readonly ILogger<FallbackChatClient> _logger;

    public FallbackChatClient(
        IChatClient innerClient,
        IProviderRegistry registry,
        AiProvider currentProvider,
        ModelComplexity complexity,
        ILogger<FallbackChatClient> logger) : base(innerClient)
    {
        _registry = registry;
        _currentProvider = currentProvider;
        _complexity = complexity;
        _logger = logger;
    }

    public override async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return await TryFallbackAsync(messages, options, cancellationToken, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return await TryFallbackAsync(messages, options, cancellationToken, ex.Message);
        }
    }

    private async Task<Microsoft.Extensions.AI.ChatResponse> TryFallbackAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken,
        string reason)
    {
        var next = _registry.GetNext(_currentProvider);
        if (next is null)
            throw new InvalidOperationException($"Primary provider {_currentProvider} failed ({reason}) and no fallback provider is available.");

        _logger.LogWarning("Provider {Current} failed. Falling back to {Fallback}. Reason: {Reason}",
            _currentProvider, next.Provider, reason);

        var fallbackClient = _registry.CreateClient(next.Provider, _complexity);
        return await fallbackClient.GetResponseAsync(messages, options, cancellationToken);
    }
}
