using System.Text.Json.Serialization;

namespace AiGateway.Api.Core.Models;

public record ChatResponse
{
    [JsonPropertyName("completion")]
    public required string Completion { get; init; }

    [JsonPropertyName("modelUsed")]
    public required string ModelUsed { get; init; }

    [JsonPropertyName("providerUsed")]
    public required AiProvider ProviderUsed { get; init; }

    [JsonPropertyName("domain")]
    public required TaskDomain Domain { get; init; }

    [JsonPropertyName("enhancedPrompt")]
    public string? EnhancedPrompt { get; init; }

    [JsonPropertyName("usage")]
    public TokenUsage? Usage { get; init; }

    [JsonPropertyName("estimatedCost")]
    public decimal? EstimatedCost { get; init; }

    [JsonPropertyName("sessionHit")]
    public bool? SessionHit { get; init; }

    [JsonPropertyName("appId")]
    public string? AppId { get; init; }

    [JsonPropertyName("agentScope")]
    public string? AgentScope { get; init; }
}

public record TokenUsage(long InputTokens, long OutputTokens, long TotalTokens);
