using Atlas.Core.Identity;
using Atlas.WebApi.Helpers;

namespace Atlas.WebApi.Middlewares;

public sealed class ClientContextMiddleware
{
    private const string ClientContextItemKey = "ClientContext";
    private readonly RequestDelegate _next;

    public ClientContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Items.ContainsKey(ClientContextItemKey))
        {
            var clientContext = ControllerHelper.GetClientContext(context);
            context.Items[ClientContextItemKey] = clientContext;
        }

        return _next(context);
    }
}
