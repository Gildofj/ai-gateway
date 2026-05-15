using AiGateway.Api.Features.AppContext;
using AiGateway.Api.Core.Models;
using Google.Cloud.Firestore;

namespace AiGateway.Api.Features.Sessions;

public class FirestoreSessionStore : ISessionStore
{
    private readonly FirestoreDb _db;
    private readonly IAppContext _appContext;

    public FirestoreSessionStore(FirestoreDb db, IAppContext appContext)
    {
        _db = db;
        _appContext = appContext;
    }

    private CollectionReference SessionCollection => _db.Collection("apps").Document(_appContext.AppId).Collection("sessions");

    public async Task<Session?> GetAsync(string sessionId)
    {
        var doc = await SessionCollection.Document(sessionId).GetSnapshotAsync();
        if (!doc.Exists) return null;

        var expiresAt = doc.GetValue<Timestamp>("expiresAt").ToDateTime();
        if (expiresAt < DateTime.UtcNow)
        {
            await doc.Reference.DeleteAsync();
            return null;
        }

        return new Session
        {
            Id = doc.Id,
            Domain = doc.ContainsField("domain") ? Enum.Parse<TaskDomain>(doc.GetValue<string>("domain")) : null,
            Complexity = doc.ContainsField("complexity") ? Enum.Parse<ModelComplexity>(doc.GetValue<string>("complexity")) : null,
            Provider = doc.ContainsField("provider") ? Enum.Parse<AiProvider>(doc.GetValue<string>("provider")) : null,
            AgentId = doc.ContainsField("agentId") ? doc.GetValue<string>("agentId") : null,
            Turns = doc.GetValue<List<Dictionary<string, object>>>("turns").Select(t => new SessionTurn
            {
                Role = (string)t["role"],
                Content = (string)t["content"],
                Timestamp = ((Timestamp)t["timestamp"]).ToDateTime()
            }).ToList(),
            TurnCount = doc.GetValue<int>("turnCount"),
            CreatedAt = doc.GetValue<Timestamp>("createdAt").ToDateTime(),
            UpdatedAt = doc.GetValue<Timestamp>("updatedAt").ToDateTime(),
            ExpiresAt = expiresAt
        };
    }

    public async Task UpsertAsync(Session session)
    {
        var data = new Dictionary<string, object>
        {
            { "turnCount", session.TurnCount },
            { "turns", session.Turns.Select(t => new Dictionary<string, object>
                {
                    { "role", t.Role },
                    { "content", t.Content },
                    { "timestamp", Timestamp.FromDateTime(t.Timestamp.ToUniversalTime()) }
                }).ToList() },
            { "createdAt", Timestamp.FromDateTime(session.CreatedAt.ToUniversalTime()) },
            { "updatedAt", Timestamp.FromDateTime(session.UpdatedAt.ToUniversalTime()) },
            { "expiresAt", Timestamp.FromDateTime(session.ExpiresAt.ToUniversalTime()) }
        };

        if (session.Domain.HasValue) data["domain"] = session.Domain.Value.ToString();
        if (session.Complexity.HasValue) data["complexity"] = session.Complexity.Value.ToString();
        if (session.Provider.HasValue) data["provider"] = session.Provider.Value.ToString();
        if (!string.IsNullOrEmpty(session.AgentId)) data["agentId"] = session.AgentId;

        await SessionCollection.Document(session.Id).SetAsync(data, SetOptions.MergeAll);
    }

    public async Task DeleteAsync(string sessionId)
    {
        await SessionCollection.Document(sessionId).DeleteAsync();
    }
}
