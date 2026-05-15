using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace AiGateway.Api.Features.AppContext;

public class AppContextMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Regex AppIdRegex = new(@"^[a-z0-9][a-z0-9-]{0,31}$", RegexOptions.Compiled);

    public AppContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var appId = context.Request.Headers["X-App-Id"].ToString();

        if (string.IsNullOrEmpty(appId))
        {
            appId = "default";
        }
        else if (!AppIdRegex.IsMatch(appId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid X-App-Id format. Must be 1-32 lowercase alphanumeric characters or hyphens, starting with alphanumeric." });
            return;
        }

        context.Items["AppId"] = appId;

        // For structured logging
        using (var scope = context.RequestServices.GetRequiredService<ILogger<AppContextMiddleware>>().BeginScope(new Dictionary<string, object> { ["AppId"] = appId }))
        {
            await _next(context);
        }
    }
}
