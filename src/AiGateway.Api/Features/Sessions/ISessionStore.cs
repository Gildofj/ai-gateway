namespace AiGateway.Api.Features.Sessions;

public interface ISessionStore
{
    Task<Session?> GetAsync(string sessionId);
    Task UpsertAsync(Session session);
    Task DeleteAsync(string sessionId);
}
