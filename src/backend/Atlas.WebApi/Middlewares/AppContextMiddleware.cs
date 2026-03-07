using Atlas.Core.Identity;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Identity;
using Microsoft.Extensions.Options;

namespace Atlas.WebApi.Middlewares;

public sealed class AppContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppOptions _options;

    public AppContextMiddleware(RequestDelegate next, IOptions<AppOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Items.ContainsKey(HttpContextAppContextAccessor.AppIdItemKey))
        {
            var claimAppId = AppIdResolver.TryResolveFromClaims(context.User);
            var headerAppId = AppIdResolver.TryResolveFromHeader(context, _options);

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                if (_options.AllowHeaderOverrideWhenAuthenticated && !string.IsNullOrWhiteSpace(headerAppId))
                {
                    context.Items[HttpContextAppContextAccessor.AppIdItemKey] = headerAppId;
                    return _next(context);
                }

                if (!string.IsNullOrWhiteSpace(claimAppId))
                {
                    context.Items[HttpContextAppContextAccessor.AppIdItemKey] = claimAppId;
                    return _next(context);
                }
            }
            else if (!string.IsNullOrWhiteSpace(headerAppId))
            {
                context.Items[HttpContextAppContextAccessor.AppIdItemKey] = headerAppId;
                return _next(context);
            }

            var clientContext = ControllerHelper.GetClientContext(context);
            context.Items[HttpContextAppContextAccessor.AppIdItemKey] = ResolveAppIdByClientType(clientContext);
        }

        return _next(context);
    }

    private string ResolveAppIdByClientType(ClientContext clientContext)
    {
        if (_options.ClientTypeMappings.Count == 0)
        {
            return _options.DefaultAppId;
        }

        var clientType = clientContext.ClientType.ToString();
        foreach (var mapping in _options.ClientTypeMappings)
        {
            if (string.Equals(mapping.ClientType, clientType, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(mapping.AppId))
            {
                return mapping.AppId.Trim();
            }
        }

        return _options.DefaultAppId;
    }

}
