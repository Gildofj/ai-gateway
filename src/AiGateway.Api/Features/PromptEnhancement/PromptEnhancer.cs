using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.PromptEnhancement;

public class PromptEnhancer : IPromptEnhancer
{
    private readonly IProviderRegistry _registry;
    private readonly ILogger<PromptEnhancer> _logger;

    public PromptEnhancer(IProviderRegistry registry, ILogger<PromptEnhancer> logger)
    {
        _registry = registry;
        _logger = logger;
        if (registry.GetAvailable().Count == 0)
            throw new InvalidOperationException("No AI providers configured.");
    }

    public async Task<string> EnhanceAsync(string prompt, string hint, CancellationToken cancellationToken = default)
    {
        try
        {
            var hintSection = string.IsNullOrWhiteSpace(hint)
                ? string.Empty
                : $" Pay special attention to: {hint}";

            var systemMessage = new ChatMessage(ChatRole.System,
                "You are a prompt engineering expert. Rewrite the user's prompt to be clearer, more specific, and better structured. " +
                "Do NOT answer it — return only the improved prompt. Remove ambiguity, add implicit context, keep it concise." +
                hintSection);

            var preferred = _registry.GetAvailable()[0].Provider;
            var response = await _registry.ExecuteAsync(
                preferred,
                ModelComplexity.Low,
                async ctx =>
                {
                    var options = new ChatOptions { ModelId = ctx.ModelName };
                    return await ctx.Client.GetResponseAsync(
                    [
                        systemMessage,
                        new ChatMessage(ChatRole.User, $"Enhance:\n{prompt}")
                    ], options, cancellationToken);
                },
                allowFallback: true,
                cancellationToken: cancellationToken);

            return response.Text ?? prompt;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prompt enhancement failed. Proceeding with original prompt.");
            return prompt;
        }
    }
}
