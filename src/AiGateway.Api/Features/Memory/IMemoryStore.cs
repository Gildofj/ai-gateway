using System.Text.Json.Serialization;

namespace AiGateway.Api.Features.Memory;

public record MemoryEntry
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = "app";

    [JsonPropertyName("ownerAppId")]
    public string? OwnerAppId { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; init; }
}

public interface IMemoryStore
{
    Task<MemoryEntry?> GetAsync(string key);
    Task SetAsync(string key, string value, string scope = "app", TimeSpan? ttl = null);
    Task DeleteAsync(string key);
    Task<IEnumerable<MemoryEntry>> ListAsync(string? prefix = null);
}
