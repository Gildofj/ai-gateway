using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.Embeddings;

public interface IEmbeddingProviderFactory
{
    AiProvider Provider { get; }
    IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(ProviderDescriptor descriptor);
}
