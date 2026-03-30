using Atlas.Application.System.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class AppDbScopeFactory : IAppDbScopeFactory
{
    private readonly ITenantDbConnectionFactory _connectionFactory;
    private readonly ISqlSugarClient _mainDb;

    public AppDbScopeFactory(
        ITenantDbConnectionFactory connectionFactory,
        ISqlSugarClient mainDb)
    {
        _connectionFactory = connectionFactory;
        _mainDb = mainDb;
    }

    public async Task<ISqlSugarClient> GetAppClientAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.AppContextRequired, "应用上下文缺失，无法解析应用级数据库连接。");
        }

        var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId, cancellationToken);
        if (policy is not null && string.Equals(policy.Mode, "MainOnly", StringComparison.OrdinalIgnoreCase))
        {
            return _mainDb;
        }

        var tenantIdValue = tenantId.Value.ToString();
        var info = await _connectionFactory.GetConnectionInfoAsync(tenantIdValue, appInstanceId, cancellationToken);
        if (info is null)
        {
            throw new BusinessException(
                ErrorCodes.ValidationError,
                $"应用实例 {appInstanceId} 未绑定可用数据源，无法访问应用数据面。");
        }

        var config = new ConnectionConfig
        {
            ConnectionString = info.ConnectionString,
            DbType = MapDbType(info.DbType),
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        };

        var db = new SqlSugarClient(config);
        db.QueryFilter.AddTableFilter<Atlas.Core.Abstractions.TenantEntity>(it => it.TenantIdValue == tenantId.Value);
        EnsureAppSchema(db);
        return db;
    }

    private static void EnsureAppSchema(ISqlSugarClient db)
    {
        // 应用数据面首次切换到新库（或缓存命中空库）时，兜底创建必需表，避免 no such table 异常。
        if (db.DbMaintenance.IsAnyTable("DynamicTable", false))
        {
            return;
        }

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

    private static DbType MapDbType(string? dbType)
    {
        return DataSourceDriverRegistry.ResolveDbType(dbType);
    }
}
