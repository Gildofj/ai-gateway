using System.Text.Json.Serialization;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Sessions;

public record Session
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("domain")]
    public TaskDomain? Domain { get; init; }

    [JsonPropertyName("complexity")]
    public ModelComplexity? Complexity { get; init; }

    [JsonPropertyName("provider")]
    public AiProvider? Provider { get; init; }

    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }

    [JsonPropertyName("turns")]
    public List<SessionTurn> Turns { get; init; } = new();

    [JsonPropertyName("turnCount")]
    public int TurnCount { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; init; }
}

public record SessionTurn
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }
}
