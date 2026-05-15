namespace AiGateway.Api.Core.Models;

public record ProviderDescriptor(
    AiProvider Provider,
    string ApiKey,
    string FastModel,
    string CapableModel,
    Uri? Endpoint = null,
    string? EmbeddingModel = "text-embedding-3-small");
