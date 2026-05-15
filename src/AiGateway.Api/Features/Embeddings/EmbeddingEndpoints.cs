using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.Embeddings;

public static class EmbeddingEndpoints
{
    public static void MapEmbeddingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/embeddings", async (
            EmbeddingRequest request,
            IProviderRegistry registry,
            IEnumerable<IEmbeddingProviderFactory> factories,
            EmbeddingCache cache,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var provider = request.Provider ?? AiProvider.OpenAi;
            var descriptor = registry.GetAvailable().FirstOrDefault(d => d.Provider == provider);

            if (descriptor == null)
            {
                return Results.BadRequest(new { error = $"Provider {provider} is not configured." });
            }

            var factory = factories.FirstOrDefault(f => f.Provider == provider);
            if (factory == null)
            {
                return Results.BadRequest(new { error = $"Embeddings not supported for provider {provider}." });
            }

            var generator = factory.CreateGenerator(descriptor);
            var model = request.Model ?? descriptor.EmbeddingModel ?? "text-embedding-3-small";
            
            var inputs = request.Input switch
            {
                string s => new[] { s },
                IEnumerable<string> list => list.ToArray(),
                _ => Array.Empty<string>()
            };

            if (inputs.Length == 0)
            {
                return Results.BadRequest(new { error = "Input is required." });
            }

            var results = new List<float[]>();
            var cacheHits = 0;

            foreach (var input in inputs)
            {
                var cached = await cache.GetAsync(model, input);
                if (cached != null)
                {
                    results.Add(cached);
                    cacheHits++;
                }
                else
                {
                    var embeddingResponse = await generator.GenerateAsync(new[] { input }, new EmbeddingGenerationOptions { ModelId = model }, cancellationToken);
                    var embedding = embeddingResponse.First().Vector.ToArray();
                    results.Add(embedding);

                    var ttlDays = configuration.GetValue("EMBEDDING_CACHE_TTL_DAYS", 7);
                    await cache.SetAsync(model, input, embedding, TimeSpan.FromDays(ttlDays));
                }
            }

            return Results.Ok(new EmbeddingResponse
            {
                Embeddings = results,
                ModelUsed = model,
                CacheHit = cacheHits == inputs.Length && inputs.Length > 0,
                Usage = new { total_inputs = inputs.Length, cache_hits = cacheHits }
            });
        });
    }
}

public record EmbeddingRequest(object Input, AiProvider? Provider = null, string? Model = null);
public record EmbeddingResponse
{
    public required IEnumerable<float[]> Embeddings { get; init; }
    public required string ModelUsed { get; init; }
    public bool CacheHit { get; init; }
    public object? Usage { get; init; }
}
