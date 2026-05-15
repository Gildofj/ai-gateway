using System.Text.Json.Serialization;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public record CustomAgent
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("domain")]
    public TaskDomain Domain { get; init; } = TaskDomain.General;

    [JsonPropertyName("preferredProviders")]
    public List<AiProvider> PreferredProviders { get; init; } = new();

    [JsonPropertyName("systemPromptFragment")]
    public required string SystemPromptFragment { get; init; }

    [JsonPropertyName("requiredSkills")]
    public List<string> RequiredSkills { get; init; } = new();

    [JsonPropertyName("enhancementHint")]
    public string? EnhancementHint { get; init; }

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = "app";

    [JsonPropertyName("ownerAppId")]
    public string? OwnerAppId { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; init; }
}
