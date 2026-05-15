using System.ClientModel;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;

namespace AiGateway.Api.Features.Embeddings;

public sealed class OpenAiEmbeddingFactory : IEmbeddingProviderFactory
{
    public AiProvider Provider => AiProvider.OpenAi;

    public IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(ProviderDescriptor descriptor)
    {
        var modelId = descriptor.EmbeddingModel ?? "text-embedding-3-small";
        var credential = new ApiKeyCredential(descriptor.ApiKey);
        var options = descriptor.Endpoint is not null
            ? new OpenAIClientOptions { Endpoint = descriptor.Endpoint }
            : new OpenAIClientOptions();

        return new EmbeddingClient(modelId, credential, options).AsIEmbeddingGenerator();
    }
}
