using System.Text.Json.Serialization;

namespace AiGateway.Api.Core.Models;

public record ChatResponse
{
    [JsonPropertyName("completion")]
    public required string Completion { get; init; }

    [JsonPropertyName("modelUsed")]
    public required string ModelUsed { get; init; }
    
    [JsonPropertyName("enhancedPrompt")]
    public string? EnhancedPrompt { get; init; }
}
