using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.DataScopes.AppRuntime;

public interface IAppSqlSugarScopeFactory
{
    Task<ISqlSugarClient> CreateAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default);
}
