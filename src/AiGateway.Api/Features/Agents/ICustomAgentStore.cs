namespace AiGateway.Api.Features.Agents;

public interface ICustomAgentStore
{
    Task<CustomAgent?> GetAsync(string id);
    Task CreateAsync(CustomAgent agent);
    Task UpdateAsync(CustomAgent agent);
    Task DeleteAsync(string id);
    Task<IEnumerable<CustomAgent>> ListAsync();
}
