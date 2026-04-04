using Atlas.Core.Identity;
using Atlas.Presentation.Shared.Helpers;
using Atlas.Presentation.Shared.Identity;
using Microsoft.Extensions.Options;

namespace Atlas.Presentation.Shared.Middlewares;

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
            var hasAuthCredentials = AppIdResolver.HasAuthenticationCredentials(context);

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(claimAppId))
                {
                    context.Items[HttpContextAppContextAccessor.AppIdItemKey] = claimAppId;
                    return _next(context);
                }

                if (AppIdResolver.CanUseHeaderOverrideForAuthenticatedRequest(context, _options)
                    && !string.IsNullOrWhiteSpace(headerAppId))
                {
                    context.Items[HttpContextAppContextAccessor.AppIdItemKey] = headerAppId;
                    return _next(context);
                }
            }
            else if (!string.IsNullOrWhiteSpace(headerAppId))
            {
                if (!hasAuthCredentials)
                {
                    context.Items[HttpContextAppContextAccessor.AppIdItemKey] = headerAppId;
                    return _next(context);
                }
            }

            if (hasAuthCredentials)
            {
                // Defer resolution for credential-carrying requests so accessor can
                // evaluate authenticated claims after authorization middleware runs.
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
