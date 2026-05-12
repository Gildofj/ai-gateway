using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.PromptEnhancement;

public class PromptEnhancer : IPromptEnhancer
{
    private readonly IChatClient _fastClient;

    public PromptEnhancer(IProviderRegistry registry)
    {
        var available = registry.GetAvailable();
        if (available.Count == 0)
            throw new InvalidOperationException("No AI providers configured.");

        _fastClient = registry.CreateClient(available[0].Provider, ModelComplexity.Low);
    }

    public async Task<string> EnhanceAsync(string prompt, string hint, CancellationToken cancellationToken = default)
    {
        var hintSection = string.IsNullOrWhiteSpace(hint)
            ? string.Empty
            : $" Pay special attention to: {hint}";

        var systemMessage = new ChatMessage(ChatRole.System,
            "You are a prompt engineering expert. Rewrite the user's prompt to be clearer, more specific, and better structured. " +
            "Do NOT answer it — return only the improved prompt. Remove ambiguity, add implicit context, keep it concise." +
            hintSection);

        var response = await _fastClient.GetResponseAsync(
        [
            systemMessage,
            new ChatMessage(ChatRole.User, $"Enhance:\n{prompt}")
        ], cancellationToken: cancellationToken);

        return response.Text ?? prompt;
    }
}
