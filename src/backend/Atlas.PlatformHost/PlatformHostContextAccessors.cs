using Atlas.Core.Identity;
using Atlas.Core.Tenancy;

namespace Atlas.PlatformHost;

internal sealed class PlatformHostCurrentUserAccessor : ICurrentUserAccessor
{
    private static readonly CurrentUserInfo SystemUser = new(
        UserId: 0,
        Username: "platform-host",
        DisplayName: "Platform Host",
        TenantId: TenantId.Empty,
        Roles: []);

    public CurrentUserInfo? GetCurrentUser()
    {
        return SystemUser;
    }

    public CurrentUserInfo GetCurrentUserOrThrow()
    {
        return SystemUser;
    }
}

internal sealed class PlatformHostAppContextAccessor : IAppContextAccessor
{
    private static readonly CurrentUserInfo SystemUser = new(
        UserId: 0,
        Username: "platform-host",
        DisplayName: "Platform Host",
        TenantId: TenantId.Empty,
        Roles: []);

    private static readonly ClientContext ClientContext = new(
        ClientType.Backend,
        ClientPlatform.Web,
        ClientChannel.Browser,
        ClientAgent.Other);

    private static readonly AppContextSnapshot Snapshot = new(
        TenantId.Empty,
        string.Empty,
        SystemUser,
        ClientContext,
        null);

    public IAppContext GetCurrent()
    {
        return Snapshot;
    }

    public string GetAppId()
    {
        return string.Empty;
    }

    public IDisposable BeginScope(IAppContext context)
    {
        return EmptyScope.Instance;
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class PlatformHostProjectContextAccessor : IProjectContextAccessor
{
    public ProjectContext GetCurrent()
    {
        return new ProjectContext(false, null);
    }
}

internal sealed class PlatformHostClientContextAccessor : IClientContextAccessor
{
    public ClientContext GetCurrent()
    {
        return new ClientContext(
            ClientType.Backend,
            ClientPlatform.Web,
            ClientChannel.Browser,
            ClientAgent.Other);
    }
}
