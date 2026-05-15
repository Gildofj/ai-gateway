using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiGateway.Api.Features.Memory;

public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/memory");

        group.MapGet("/", async (IMemoryStore store, string? prefix) =>
        {
            var entries = await store.ListAsync(prefix);
            return Results.Ok(entries);
        });

        group.MapGet("/{key}", async (IMemoryStore store, string key) =>
        {
            var entry = await store.GetAsync(key);
            return entry != null ? Results.Ok(entry) : Results.NotFound();
        });

        group.MapPut("/{key}", async (IMemoryStore store, string key, MemoryPutRequest request) =>
        {
            try
            {
                await store.SetAsync(key, request.Value, request.Scope ?? "app", request.TtlMinutes.HasValue ? TimeSpan.FromMinutes(request.TtlMinutes.Value) : null);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

        group.MapDelete("/{key}", async (IMemoryStore store, string key) =>
        {
            try
            {
                await store.DeleteAsync(key);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });
    }
}

public record MemoryPutRequest(string Value, string? Scope = "app", int? TtlMinutes = null);
