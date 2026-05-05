namespace AiGateway.Api.Core.Interfaces;

public interface IPromptEnhancer
{
    /// <summary>
    /// Takes an initial prompt and returns an enhanced version optimized for the target AI model.
    /// </summary>
    Task<string> EnhancePromptAsync(string initialPrompt, CancellationToken cancellationToken = default);
}
