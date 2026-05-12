namespace AiGateway.Api.Core.Interfaces;

public interface IPromptEnhancer
{
    Task<string> EnhanceAsync(string prompt, string hint, CancellationToken cancellationToken = default);
}
