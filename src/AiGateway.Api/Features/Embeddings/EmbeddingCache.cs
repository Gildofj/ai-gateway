using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.Embeddings;

public class EmbeddingCache
{
    private readonly FirestoreDb _db;
    private readonly ILogger<EmbeddingCache> _logger;

    public EmbeddingCache(FirestoreDb db, ILogger<EmbeddingCache> logger)
    {
        _db = db;
        _logger = logger;
    }

    private CollectionReference CacheCollection => _db.Collection("shared").Document("global").Collection("embeddings_cache");

    public async Task<float[]?> GetAsync(string model, string input)
    {
        var hash = ComputeHash(model, input);
        try
        {
            var doc = await CacheCollection.Document(hash).GetSnapshotAsync();
            if (doc.Exists && (!doc.ContainsField("expiresAt") || doc.GetValue<Timestamp>("expiresAt").ToDateTime() > DateTime.UtcNow))
            {
                return doc.GetValue<List<float>>("embedding").ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read from embedding cache for hash {Hash}", hash);
        }

        return null;
    }

    public async Task SetAsync(string model, string input, float[] embedding, TimeSpan ttl)
    {
        var hash = ComputeHash(model, input);
        var data = new Dictionary<string, object>
        {
            { "model", model },
            { "input_preview", input.Length > 100 ? input.Substring(0, 100) : input },
            { "embedding", embedding.ToList() },
            { "createdAt", Timestamp.FromDateTime(DateTime.UtcNow) },
            { "expiresAt", Timestamp.FromDateTime(DateTime.UtcNow.Add(ttl)) }
        };

        try
        {
            // Best effort write
            await CacheCollection.Document(hash).SetAsync(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write to embedding cache for hash {Hash}", hash);
        }
    }

    private static string ComputeHash(string model, string input)
    {
        var combined = $"{model}:{input}";
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
