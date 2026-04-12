using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.AppHost.Sdk.Hosting;

namespace Atlas.AppHost;

internal sealed class AppHostTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public AppHostTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public TenantId GetTenantId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return TenantId.Empty;
        }

        var claimValue = httpContext.User.FindFirstValue("tenant_id");
        if (Guid.TryParse(claimValue, out var tenantGuid))
        {
            return new TenantId(tenantGuid);
        }

        var headerValue = httpContext.Request.Headers["X-Tenant-Id"].ToString();
        if (Guid.TryParse(headerValue, out tenantGuid))
        {
            return new TenantId(tenantGuid);
        }

        return TenantId.Empty;
    }
}

internal sealed class AppHostCurrentUserAccessor : ICurrentUserAccessor
{
    private const string DisplayNameClaimType = "display_name";
    private const string SessionIdClaimType = "sid";
    private const string PlatformAdminClaimType = "is_platform_admin";

    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ITenantProvider tenantProvider;

    public AppHostCurrentUserAccessor(IHttpContextAccessor httpContextAccessor, ITenantProvider tenantProvider)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.tenantProvider = tenantProvider;
    }

    public CurrentUserInfo? GetCurrentUser()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdText = user.FindFirstValue("user_id")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!long.TryParse(userIdText, out var userId))
        {
            return null;
        }

        var username = user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? string.Empty;
        var displayName = user.FindFirstValue(DisplayNameClaimType) ?? username;
        var roles = user.FindAll(ClaimTypes.Role)
            .Select(item => item.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sessionId = 0L;
        var sessionIdText = user.FindFirstValue(SessionIdClaimType);
        if (!string.IsNullOrWhiteSpace(sessionIdText))
        {
            _ = long.TryParse(sessionIdText, out sessionId);
        }

        var isPlatformAdmin = false;
        var platformAdminText = user.FindFirstValue(PlatformAdminClaimType);
        if (!string.IsNullOrWhiteSpace(platformAdminText))
        {
            _ = bool.TryParse(platformAdminText, out isPlatformAdmin);
        }

        return new CurrentUserInfo(
            userId,
            username,
            displayName,
            tenantProvider.GetTenantId(),
            roles,
            isPlatformAdmin,
            sessionId);
    }

    public CurrentUserInfo GetCurrentUserOrThrow()
    {
        return GetCurrentUser() ?? throw new UnauthorizedAccessException("未登录或无法解析当前用户信息。");
    }
}

internal sealed class AppHostAppContextAccessor : IAppContextAccessor
{
    private static readonly AsyncLocal<IAppContext?> CurrentScope = new();

    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ITenantProvider tenantProvider;
    private readonly ICurrentUserAccessor currentUserAccessor;
    private readonly IClientContextAccessor clientContextAccessor;
    private readonly AppInstanceConfigurationLoader appInstanceConfigurationLoader;

    public AppHostAppContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        AppInstanceConfigurationLoader appInstanceConfigurationLoader)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.tenantProvider = tenantProvider;
        this.currentUserAccessor = currentUserAccessor;
        this.clientContextAccessor = clientContextAccessor;
        this.appInstanceConfigurationLoader = appInstanceConfigurationLoader;
    }

    public IAppContext GetCurrent()
    {
        if (CurrentScope.Value is not null)
        {
            return CurrentScope.Value;
        }

        var httpContext = httpContextAccessor.HttpContext;
        return new AppContextSnapshot(
            tenantProvider.GetTenantId(),
            ResolveAppId(httpContext),
            currentUserAccessor.GetCurrentUser(),
            clientContextAccessor.GetCurrent(),
            httpContext?.TraceIdentifier);
    }

    public string GetAppId()
    {
        return GetCurrent().AppId;
    }

    private string ResolveAppId(HttpContext? httpContext)
    {
        var headerValue = httpContext?.Request.Headers["X-App-Id"].ToString();
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue;
        }

        return appInstanceConfigurationLoader.Load().InstanceId?.Trim() ?? string.Empty;
    }

    public IDisposable BeginScope(IAppContext context)
    {
        var previous = CurrentScope.Value;
        CurrentScope.Value = context;
        return new Scope(() => CurrentScope.Value = previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action onDispose;
        private bool disposed;

        public Scope(Action onDispose)
        {
            this.onDispose = onDispose;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            onDispose();
        }
    }
}

internal sealed class AppHostProjectContextAccessor : IProjectContextAccessor
{
    public ProjectContext GetCurrent()
    {
        return new ProjectContext(false, null);
    }
}

internal sealed class AppHostClientContextAccessor : IClientContextAccessor
{
    public ClientContext GetCurrent()
    {
        return new ClientContext(
            ClientType.WebH5,
            ClientPlatform.Web,
            ClientChannel.Browser,
            ClientAgent.Other);
    }
}
