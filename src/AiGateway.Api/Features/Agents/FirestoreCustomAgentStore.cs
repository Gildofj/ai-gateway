using AiGateway.Api.Features.AppContext;
using AiGateway.Api.Core.Models;
using Google.Cloud.Firestore;

namespace AiGateway.Api.Features.Agents;

public class FirestoreCustomAgentStore : ICustomAgentStore
{
    private readonly FirestoreDb _db;
    private readonly IAppContext _appContext;

    public FirestoreCustomAgentStore(FirestoreDb db, IAppContext appContext)
    {
        _db = db;
        _appContext = appContext;
    }

    private CollectionReference AppCollection => _db.Collection("apps").Document(_appContext.AppId).Collection("agents");
    private CollectionReference SharedCollection => _db.Collection("shared").Document("global").Collection("agents");

    public async Task<CustomAgent?> GetAsync(string id)
    {
        var appDoc = await AppCollection.Document(id).GetSnapshotAsync();
        if (appDoc.Exists) return Map(appDoc, "app");

        var sharedDoc = await SharedCollection.Document(id).GetSnapshotAsync();
        if (sharedDoc.Exists) return Map(sharedDoc, "global");

        return null;
    }

    public async Task CreateAsync(CustomAgent agent)
    {
        var data = MapToFirestore(agent);
        data["createdAt"] = Timestamp.FromDateTime(DateTime.UtcNow);
        data["ownerAppId"] = _appContext.AppId;

        if (agent.Scope == "global")
        {
            await SharedCollection.Document(agent.Id).SetAsync(data);
        }
        else
        {
            await AppCollection.Document(agent.Id).SetAsync(data);
        }
    }

    public async Task UpdateAsync(CustomAgent agent)
    {
        var data = MapToFirestore(agent);
        data["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow);

        if (agent.Scope == "global")
        {
            var docRef = SharedCollection.Document(agent.Id);
            var snapshot = await docRef.GetSnapshotAsync();
            if (snapshot.Exists && snapshot.GetValue<string>("ownerAppId") != _appContext.AppId)
            {
                throw new UnauthorizedAccessException($"App '{_appContext.AppId}' is not the owner of global agent '{agent.Id}'.");
            }
            await docRef.SetAsync(data, SetOptions.MergeAll);
        }
        else
        {
            await AppCollection.Document(agent.Id).SetAsync(data, SetOptions.MergeAll);
        }
    }

    public async Task DeleteAsync(string id)
    {
        var appDoc = AppCollection.Document(id);
        var appSnapshot = await appDoc.GetSnapshotAsync();
        if (appSnapshot.Exists)
        {
            await appDoc.DeleteAsync();
            return;
        }

        var sharedDoc = SharedCollection.Document(id);
        var sharedSnapshot = await sharedDoc.GetSnapshotAsync();
        if (sharedSnapshot.Exists)
        {
            if (sharedSnapshot.GetValue<string>("ownerAppId") != _appContext.AppId)
            {
                throw new UnauthorizedAccessException($"App '{_appContext.AppId}' is not the owner of global agent '{id}'.");
            }
            await sharedDoc.DeleteAsync();
        }
    }

    public async Task<IEnumerable<CustomAgent>> ListAsync()
    {
        var appAgents = await GetEntries(AppCollection, "app");
        var sharedAgents = await GetEntries(SharedCollection, "global");

        var result = new Dictionary<string, CustomAgent>();
        foreach (var agent in sharedAgents) result[agent.Id] = agent;
        foreach (var agent in appAgents) result[agent.Id] = agent;

        return result.Values;
    }

    private async Task<List<CustomAgent>> GetEntries(CollectionReference collection, string scope)
    {
        var snapshot = await collection.GetSnapshotAsync();
        return snapshot.Documents.Select(d => Map(d, scope)).ToList();
    }

    private CustomAgent Map(DocumentSnapshot doc, string scope)
    {
        return new CustomAgent
        {
            Id = doc.Id,
            Name = doc.GetValue<string>("name"),
            Description = doc.ContainsField("description") ? doc.GetValue<string>("description") : null,
            Domain = doc.ContainsField("domain") ? Enum.Parse<TaskDomain>(doc.GetValue<string>("domain")) : TaskDomain.General,
            PreferredProviders = doc.ContainsField("preferredProviders") 
                ? doc.GetValue<List<string>>("preferredProviders").Select(p => Enum.Parse<AiProvider>(p)).ToList() 
                : new List<AiProvider>(),
            SystemPromptFragment = doc.GetValue<string>("systemPromptFragment"),
            RequiredSkills = doc.ContainsField("requiredSkills") ? doc.GetValue<List<string>>("requiredSkills") : new List<string>(),
            EnhancementHint = doc.ContainsField("enhancementHint") ? doc.GetValue<string>("enhancementHint") : null,
            Scope = scope,
            OwnerAppId = doc.GetValue<string>("ownerAppId"),
            CreatedAt = doc.ContainsField("createdAt") ? doc.GetValue<Timestamp>("createdAt").ToDateTime() : null,
            UpdatedAt = doc.ContainsField("updatedAt") ? doc.GetValue<Timestamp>("updatedAt").ToDateTime() : null
        };
    }

    private Dictionary<string, object> MapToFirestore(CustomAgent agent)
    {
        return new Dictionary<string, object>
        {
            { "name", agent.Name },
            { "description", agent.Description ?? "" },
            { "domain", agent.Domain.ToString() },
            { "preferredProviders", agent.PreferredProviders.Select(p => p.ToString()).ToList() },
            { "systemPromptFragment", agent.SystemPromptFragment },
            { "requiredSkills", agent.RequiredSkills },
            { "enhancementHint", agent.EnhancementHint ?? "" }
        };
    }
}
