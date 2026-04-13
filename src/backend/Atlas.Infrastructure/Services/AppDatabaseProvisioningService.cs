using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 应用独立库初始化服务（数据面）。
/// </summary>
public sealed class AppDatabaseProvisioningService
{
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public AppDatabaseProvisioningService(IAppDbScopeFactory appDbScopeFactory)
    {
        _appDbScopeFactory = appDbScopeFactory;
    }

    public async Task EnsureSchemaAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        var db = await _appDbScopeFactory.GetAppClientAsync(tenantId, appInstanceId, cancellationToken);
        AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db);
    }
}
