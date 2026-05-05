using AiGateway.Api.Core.Interfaces;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.PromptEnhancement;

public class PromptEnhancer : IPromptEnhancer
{
    private readonly IChatClient _enhancementClient;

    public PromptEnhancer([FromKeyedServices("fast-model")] IChatClient enhancementClient)
    {
        _enhancementClient = enhancementClient;
    }

    public async Task<string> EnhancePromptAsync(string initialPrompt, CancellationToken cancellationToken = default)
    {
        var systemMessage = new ChatMessage(ChatRole.System, 
            "You are a Prompt Engineering expert. Your task is to take the user's prompt and rewrite it to be clearer, more specific, and better structured for an AI to understand. " +
            "Do not answer the prompt, just return the improved prompt. Keep it concise but add necessary context if it's implicitly missing.");

        var userMessage = new ChatMessage(ChatRole.User, $"Enhance this prompt:\n{initialPrompt}");

        var response = await _enhancementClient.GetResponseAsync(new[] { systemMessage, userMessage }, cancellationToken: cancellationToken);
        
        return response.Text ?? initialPrompt;
    }
}
