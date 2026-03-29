using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 应用数据面数据库客户端工厂。
/// </summary>
public interface IAppDbScopeFactory
{
    Task<ISqlSugarClient> GetAppClientAsync(TenantId tenantId, long appInstanceId, CancellationToken cancellationToken = default);
}
