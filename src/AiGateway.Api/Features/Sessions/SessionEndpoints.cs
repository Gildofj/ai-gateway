using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiGateway.Api.Features.Sessions;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sessions");

        group.MapGet("/{id}", async (ISessionStore store, string id) =>
        {
            var session = await store.GetAsync(id);
            return session != null ? Results.Ok(session) : Results.NotFound();
        });

        group.MapDelete("/{id}", async (ISessionStore store, string id) =>
        {
            await store.DeleteAsync(id);
            return Results.NoContent();
        });
    }
}
