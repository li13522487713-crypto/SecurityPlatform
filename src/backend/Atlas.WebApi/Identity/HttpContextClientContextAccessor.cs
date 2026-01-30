using Atlas.Core.Identity;
using Atlas.WebApi.Helpers;

namespace Atlas.WebApi.Identity;

public sealed class HttpContextClientContextAccessor : IClientContextAccessor
{
    private const string ClientContextItemKey = "ClientContext";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextClientContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ClientContext GetCurrent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return GetDefault();
        }

        if (httpContext.Items.TryGetValue(ClientContextItemKey, out var value)
            && value is ClientContext cached)
        {
            return cached;
        }

        var resolved = ControllerHelper.GetClientContext(httpContext);
        httpContext.Items[ClientContextItemKey] = resolved;
        return resolved;
    }

    private static ClientContext GetDefault()
    {
        return new ClientContext(ClientType.WebH5, ClientPlatform.Web, ClientChannel.Browser, ClientAgent.Other);
    }
}
