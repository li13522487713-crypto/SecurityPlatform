using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

internal sealed class MainOnlyAppDbScopeFactory : IAppDbScopeFactory
{
    private readonly ISqlSugarClient _mainDb;

    public MainOnlyAppDbScopeFactory(ISqlSugarClient mainDb)
    {
        _mainDb = mainDb;
    }

    public Task<ISqlSugarClient> GetAppClientAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_mainDb);
    }

    public void InvalidateAppClientCache(TenantId tenantId, long appInstanceId)
    {
    }
}

internal sealed class NullAppContextAccessor : IAppContextAccessor
{
    public static readonly NullAppContextAccessor Instance = new();

    private NullAppContextAccessor()
    {
    }

    public IAppContext GetCurrent()
    {
        return new AppContextSnapshot(
            TenantId.Empty,
            string.Empty,
            null,
            new ClientContext(ClientType.Backend, ClientPlatform.Web, ClientChannel.Browser, ClientAgent.Other),
            null);
    }

    public string GetAppId()
    {
        return string.Empty;
    }

    public IDisposable BeginScope(IAppContext context)
    {
        return NullScope.Instance;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
