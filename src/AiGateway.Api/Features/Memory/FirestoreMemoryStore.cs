using AiGateway.Api.Features.AppContext;
using Google.Cloud.Firestore;

namespace AiGateway.Api.Features.Memory;

public class FirestoreMemoryStore : IMemoryStore
{
    private readonly FirestoreDb _db;
    private readonly IAppContext _appContext;

    public FirestoreMemoryStore(FirestoreDb db, IAppContext appContext)
    {
        _db = db;
        _appContext = appContext;
    }

    private CollectionReference AppCollection => _db.Collection("apps").Document(_appContext.AppId).Collection("memory");
    private CollectionReference SharedCollection => _db.Collection("shared").Document("global").Collection("memory");

    public async Task<MemoryEntry?> GetAsync(string key)
    {
        // Try app-scoped first
        var appDoc = await AppCollection.Document(key).GetSnapshotAsync();
        if (appDoc.Exists)
        {
            return Map(appDoc, "app");
        }

        // Fallback to shared
        var sharedDoc = await SharedCollection.Document(key).GetSnapshotAsync();
        if (sharedDoc.Exists)
        {
            return Map(sharedDoc, "global");
        }

        return null;
    }

    public async Task SetAsync(string key, string value, string scope = "app", TimeSpan? ttl = null)
    {
        var data = new Dictionary<string, object>
        {
            { "key", key },
            { "value", value },
            { "updatedAt", Timestamp.FromDateTime(DateTime.UtcNow) },
            { "ownerAppId", _appContext.AppId }
        };

        if (ttl.HasValue)
        {
            data["expiresAt"] = Timestamp.FromDateTime(DateTime.UtcNow.Add(ttl.Value));
        }

        if (scope == "global")
        {
            var docRef = SharedCollection.Document(key);
            var snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists && snapshot.GetValue<string>("ownerAppId") != _appContext.AppId)
            {
                throw new UnauthorizedAccessException($"App '{_appContext.AppId}' is not the owner of global memory entry '{key}'.");
            }

            await docRef.SetAsync(data);
        }
        else
        {
            await AppCollection.Document(key).SetAsync(data);
        }
    }

    public async Task DeleteAsync(string key)
    {
        // Check app-scoped first
        var appDoc = AppCollection.Document(key);
        var appSnapshot = await appDoc.GetSnapshotAsync();
        if (appSnapshot.Exists)
        {
            await appDoc.DeleteAsync();
            return;
        }

        // Then check shared
        var sharedDoc = SharedCollection.Document(key);
        var sharedSnapshot = await sharedDoc.GetSnapshotAsync();
        if (sharedSnapshot.Exists)
        {
            if (sharedSnapshot.GetValue<string>("ownerAppId") != _appContext.AppId)
            {
                throw new UnauthorizedAccessException($"App '{_appContext.AppId}' is not the owner of global memory entry '{key}'.");
            }
            await sharedDoc.DeleteAsync();
        }
    }

    public async Task<IEnumerable<MemoryEntry>> ListAsync(string? prefix = null)
    {
        var appEntries = await GetEntries(AppCollection, "app", prefix);
        var sharedEntries = await GetEntries(SharedCollection, "global", prefix);

        // Combine and handle shadowing (app-scoped wins)
        var result = new Dictionary<string, MemoryEntry>();

        foreach (var entry in sharedEntries)
        {
            result[entry.Key] = entry;
        }

        foreach (var entry in appEntries)
        {
            result[entry.Key] = entry;
        }

        return result.Values;
    }

    private async Task<List<MemoryEntry>> GetEntries(CollectionReference collection, string scope, string? prefix)
    {
        Query query = collection;
        if (!string.IsNullOrEmpty(prefix))
        {
            query = query.WhereGreaterThanOrEqualTo("key", prefix).WhereLessThan("key", prefix + "\uf8ff");
        }

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Select(d => Map(d, scope)).ToList();
    }

    private MemoryEntry Map(DocumentSnapshot doc, string scope)
    {
        return new MemoryEntry
        {
            Key = doc.GetValue<string>("key"),
            Value = doc.GetValue<string>("value"),
            Scope = scope,
            OwnerAppId = doc.GetValue<string>("ownerAppId"),
            UpdatedAt = doc.GetValue<Timestamp>("updatedAt").ToDateTime(),
            ExpiresAt = doc.ContainsField("expiresAt") ? doc.GetValue<Timestamp>("expiresAt").ToDateTime() : null
        };
    }
}
