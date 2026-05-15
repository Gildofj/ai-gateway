using AiGateway.Api.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiGateway.Api.Features.Agents;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/agents");

        group.MapGet("/", async (ICustomAgentStore store, IEnumerable<IDomainAgent> builtInAgents) =>
        {
            var customAgents = await store.ListAsync();
            
            // Note: builtInAgents here are the ones registered in DI.
            // We might want to return them in the list too, but maybe with a special flag.
            
            return Results.Ok(new {
                custom = customAgents,
                builtIn = builtInAgents.Select(a => new {
                    id = a.GetType().Name.Replace("Agent", "").ToLower(),
                    domain = a.Domain,
                    preferredProviders = a.PreferredProviders
                })
            });
        });

        group.MapGet("/{id}", async (ICustomAgentStore store, string id) =>
        {
            var agent = await store.GetAsync(id);
            return agent != null ? Results.Ok(agent) : Results.NotFound();
        });

        group.MapPost("/", async (ICustomAgentStore store, CustomAgent agent) =>
        {
            await store.CreateAsync(agent);
            return Results.Created($"/api/v1/agents/{agent.Id}", agent);
        });

        group.MapPut("/{id}", async (ICustomAgentStore store, string id, CustomAgent agent) =>
        {
            if (id != agent.Id) return Results.BadRequest("ID mismatch");
            
            try
            {
                await store.UpdateAsync(agent);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        group.MapDelete("/{id}", async (ICustomAgentStore store, string id) =>
        {
            try
            {
                await store.DeleteAsync(id);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });
    }
}
