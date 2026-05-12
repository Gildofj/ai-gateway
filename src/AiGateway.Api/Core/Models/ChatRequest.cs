using System.Text.Json.Serialization;

namespace AiGateway.Api.Core.Models;

public record ChatRequest
{
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("domain")]
    public TaskDomain? Domain { get; init; }

    [JsonPropertyName("complexity")]
    public ModelComplexity? Complexity { get; init; }

    [JsonPropertyName("provider")]
    public AiProvider? Provider { get; init; }

    [JsonPropertyName("enablePromptEnhancement")]
    public bool EnablePromptEnhancement { get; init; } = true;

    [JsonPropertyName("useSkills")]
    public bool UseSkills { get; init; } = true;
}
