using Microsoft.AspNetCore.Http;

namespace AiGateway.Api.Features.AppContext;

public class HttpAppContextAccessor : IAppContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpAppContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string AppId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "default";

            if (context.Items.TryGetValue("AppId", out var appId) && appId is string appIdString)
            {
                return appIdString;
            }

            return "default";
        }
    }
}
