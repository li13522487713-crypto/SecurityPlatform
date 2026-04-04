using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.DataScopes.AppRuntime;

public sealed class AppSqlSugarScopeFactory : IAppSqlSugarScopeFactory
{
    private readonly IAppDbScopeFactory appDbScopeFactory;

    public AppSqlSugarScopeFactory(IAppDbScopeFactory appDbScopeFactory)
    {
        this.appDbScopeFactory = appDbScopeFactory;
    }

    public Task<ISqlSugarClient> CreateAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        return appDbScopeFactory.GetAppClientAsync(tenantId, appInstanceId, cancellationToken);
    }
}
