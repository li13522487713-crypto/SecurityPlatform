using Atlas.Application.System.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
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
            IsAutoCloseConnection = true
        };

        var db = new SqlSugarClient(config);
        db.QueryFilter.AddTableFilter<Atlas.Core.Abstractions.TenantEntity>(it => it.TenantIdValue == tenantId.Value);
        return db;
    }

    private static DbType MapDbType(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType))
        {
            return DbType.Sqlite;
        }

        return dbType.Trim().ToLowerInvariant() switch
        {
            "sqlite" => DbType.Sqlite,
            "sqlserver" => DbType.SqlServer,
            "mysql" => DbType.MySql,
            "postgresql" => DbType.PostgreSQL,
            _ => DbType.Sqlite
        };
    }
}
