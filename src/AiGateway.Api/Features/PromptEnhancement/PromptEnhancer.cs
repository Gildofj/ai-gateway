using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.PromptEnhancement;

public class PromptEnhancer : IPromptEnhancer
{
    private readonly IChatClient _fastClient;
    private readonly ILogger<PromptEnhancer> _logger;

    public PromptEnhancer(IProviderRegistry registry, ILogger<PromptEnhancer> logger)
    {
        _logger = logger;
        var available = registry.GetAvailable();
        if (available.Count == 0)
            throw new InvalidOperationException("No AI providers configured.");

        _fastClient = registry.CreateResilientClient(available[0].Provider, ModelComplexity.Low, logger);
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

            var response = await _fastClient.GetResponseAsync(
            [
                systemMessage,
                new ChatMessage(ChatRole.User, $"Enhance:\n{prompt}")
            ], cancellationToken: cancellationToken);

            return response.Text ?? prompt;
        }
        catch (Exception ex)
        {
            // Prompt enhancement is a "nice to have" step. If it fails (e.g. 401 on OpenAI or 411 on Google),
            // we log it and proceed with the original prompt to avoid failing the entire request.
            _logger.LogWarning(ex, "Prompt enhancement failed. Proceeding with original prompt.");
            return prompt;
        }
    }
}
