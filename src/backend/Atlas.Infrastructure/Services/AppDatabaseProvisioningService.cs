using Atlas.Core.Tenancy;
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
        db.CodeFirst.InitTables(
            typeof(DynamicTable),
            typeof(DynamicField),
            typeof(DynamicIndex),
            typeof(DynamicRelation),
            typeof(FieldPermission),
            typeof(MigrationRecord),
            typeof(DynamicSchemaMigration),
            typeof(AppMember),
            typeof(AppRole),
            typeof(AppUserRole),
            typeof(AppRolePermission),
            typeof(AppPermission),
            typeof(AppRolePage),
            typeof(AppDepartment),
            typeof(AppPosition),
            typeof(AppProject),
            typeof(RuntimeRoute),
            typeof(AppDatabaseSchemaVersion));
    }
}
