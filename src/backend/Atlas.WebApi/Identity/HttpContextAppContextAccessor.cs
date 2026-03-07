using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Options;

namespace Atlas.WebApi.Identity;

public sealed class HttpContextAppContextAccessor : IAppContextAccessor
{
    public const string AppIdItemKey = "AppContext.AppId";

    private static readonly AsyncLocal<IAppContext?> OverrideContext = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppOptions _options;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;

    public HttpContextAppContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AppOptions> options,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
    }

    public IAppContext GetCurrent()
    {
        if (OverrideContext.Value is not null)
        {
            return OverrideContext.Value;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var appId = GetAppId();
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var clientContext = _clientContextAccessor.GetCurrent();
        var traceId = _httpContextAccessor.HttpContext?.TraceIdentifier;

        return new AppContextSnapshot(tenantId, appId, currentUser, clientContext, traceId);
    }

    public string GetAppId()
    {
        if (OverrideContext.Value is not null)
        {
            return OverrideContext.Value.AppId;
        }

        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return _options.DefaultAppId;
        }

        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        if (isAuthenticated)
        {
            var claimAppId = AppIdResolver.TryResolveFromClaims(context.User);
            if (!string.IsNullOrWhiteSpace(claimAppId))
            {
                return claimAppId;
            }

            if (context.Items.TryGetValue(AppIdItemKey, out var value) && value is string cached)
            {
                return cached;
            }

            if (AppIdResolver.CanUseHeaderOverrideForAuthenticatedRequest(context, _options))
            {
                var headerAppId = AppIdResolver.TryResolveFromHeader(context, _options);
                if (!string.IsNullOrWhiteSpace(headerAppId))
                {
                    return headerAppId;
                }
            }
        }
        else
        {
            if (context.Items.TryGetValue(AppIdItemKey, out var value) && value is string cached)
            {
                return cached;
            }

            var headerAppId = AppIdResolver.TryResolveFromHeader(context, _options);
            if (!string.IsNullOrWhiteSpace(headerAppId))
            {
                return headerAppId;
            }
        }

        return _options.DefaultAppId;
    }

    public IDisposable BeginScope(IAppContext context)
    {
        var previous = OverrideContext.Value;
        OverrideContext.Value = context;
        return new Scope(() => OverrideContext.Value = previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public Scope(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dispose();
            _disposed = true;
        }
    }
}
