using Atlas.Application.System.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Infrastructure.Services;

public sealed class AppDbScopeFactory : IAppDbScopeFactory
{
    private readonly ITenantDbConnectionFactory _connectionFactory;
    private readonly ISqlSugarClient _mainDb;
    private readonly IMemoryCache _cache;

    // 缓存已完成 Schema 初始化的 (tenantId:appInstanceId) 组合，进程生命周期内有效
    private static readonly ConcurrentDictionary<string, bool> _schemaInitializedKeys
        = new(StringComparer.OrdinalIgnoreCase);

    // 按 (tenantId:appId) 缓存 SqlSugarScope（线程安全单例），避免并发请求对同一 SQLite 文件各自建连接导致写锁冲突
    private static readonly ConcurrentDictionary<string, ISqlSugarClient> _clientCache
        = new(StringComparer.OrdinalIgnoreCase);

    // 缓存路由策略查询结果，避免每次请求都查主库
    private const string RoutePolicyCachePrefix = "app-route-policy:";
    private static readonly TimeSpan RoutePolicyCacheDuration = TimeSpan.FromMinutes(5);

    public AppDbScopeFactory(
        ITenantDbConnectionFactory connectionFactory,
        ISqlSugarClient mainDb,
        IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _mainDb = mainDb;
        _cache = cache;
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

        // 缓存路由策略，避免每次查主库
        var policyCacheKey = $"{RoutePolicyCachePrefix}{tenantId.Value}:{appInstanceId}";
        if (!_cache.TryGetValue(policyCacheKey, out bool isMainOnly))
        {
            var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
                .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId, cancellationToken);
            isMainOnly = policy is not null && string.Equals(policy.Mode, "MainOnly", StringComparison.OrdinalIgnoreCase);
            _cache.Set(policyCacheKey, isMainOnly, RoutePolicyCacheDuration);
        }

        if (isMainOnly)
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

        var dbType = MapDbType(info.DbType);
        var schemaKey = $"{tenantId.Value}:{appInstanceId}";

        // 复用已缓存的 SqlSugarScope；SqlSugarScope 内部使用 ThreadLocal 连接池，线程安全
        var db = _clientCache.GetOrAdd(schemaKey, _ =>
        {
            var config = new ConnectionConfig
            {
                ConnectionString = info.ConnectionString,
                DbType = dbType,
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

            var scope = new SqlSugarScope(config);
            scope.QueryFilter.AddTableFilter<Atlas.Core.Abstractions.TenantEntity>(it => it.TenantIdValue == tenantId.Value);

            // SQLite：启用 WAL 日志模式（读写不互斥）+ 写锁等待 5 s（避免并发写立即报 database is locked）
            if (dbType == DbType.Sqlite)
            {
                scope.Ado.ExecuteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
            }

            return scope;
        });

        // 只在进程首次访问该应用库时执行 Schema 检查，后续跳过
        if (!_schemaInitializedKeys.ContainsKey(schemaKey))
        {
            EnsureAppSchema(db);
            _schemaInitializedKeys.TryAdd(schemaKey, true);
        }

        return db;
    }

    private static void EnsureAppSchema(ISqlSugarClient db)
    {
        // 应用数据面首次切换到新库（或缓存命中空库）时，兜底创建必需表，避免 no such table 异常。
        if (!db.DbMaintenance.IsAnyTable("DynamicTable", false))
        {
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
        else
        {
            // 兼容历史库：旧版本可能将 DynamicTable 的可空字段建成 NOT NULL，导致插入/解绑审批流失败。
            EnsureDynamicTableNullableColumns(db);
        }
    }

    private static void EnsureDynamicTableNullableColumns(ISqlSugarClient db)
    {
        var pragma = db.Ado.GetDataTable("PRAGMA table_info(\"DynamicTable\");");
        if (pragma is null || pragma.Rows.Count == 0)
        {
            return;
        }

        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notNullColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Data.DataRow row in pragma.Rows)
        {
            var name = row["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            columnNames.Add(name);
            var notNull = Convert.ToInt32(row["notnull"]) == 1;
            if (notNull)
            {
                notNullColumns.Add(name);
            }
        }

        var shouldRebuild =
            notNullColumns.Contains(nameof(DynamicTable.Description)) ||
            notNullColumns.Contains(nameof(DynamicTable.AppId)) ||
            notNullColumns.Contains(nameof(DynamicTable.ApprovalFlowDefinitionId)) ||
            notNullColumns.Contains(nameof(DynamicTable.ApprovalStatusField));

        if (!shouldRebuild)
        {
            return;
        }

        var result = db.Ado.UseTran(() =>
        {
            db.Ado.ExecuteCommand("ALTER TABLE \"DynamicTable\" RENAME TO \"DynamicTable__old\";");
            db.CodeFirst.InitTables(typeof(DynamicTable));

            var copySql = $@"
INSERT INTO ""DynamicTable"" (
    ""TableKey"", ""DisplayName"", ""Description"", ""DbType"", ""Status"", ""CreatedAt"", ""UpdatedAt"",
    ""CreatedBy"", ""UpdatedBy"", ""AppId"", ""ApprovalFlowDefinitionId"", ""ApprovalStatusField"",
    ""TenantIdValue"", ""Id""
)
SELECT
    ""TableKey"",
    ""DisplayName"",
    {BuildSourceExpression(columnNames, "Description", "NULLIF(\"Description\", '')")},
    ""DbType"",
    ""Status"",
    ""CreatedAt"",
    ""UpdatedAt"",
    ""CreatedBy"",
    ""UpdatedBy"",
    {BuildSourceExpression(columnNames, "AppId", "NULLIF(\"AppId\", 0)")},
    {BuildSourceExpression(columnNames, "ApprovalFlowDefinitionId", "NULLIF(\"ApprovalFlowDefinitionId\", 0)")},
    {BuildSourceExpression(columnNames, "ApprovalStatusField", "NULLIF(\"ApprovalStatusField\", '')")},
    ""TenantIdValue"",
    ""Id""
FROM ""DynamicTable__old"";
";
            db.Ado.ExecuteCommand(copySql);
            db.Ado.ExecuteCommand("DROP TABLE \"DynamicTable__old\";");
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("修复 DynamicTable 兼容结构失败。");
        }
    }

    private static string BuildSourceExpression(HashSet<string> existingColumns, string columnName, string expression)
    {
        return existingColumns.Contains(columnName) ? expression : "NULL";
    }

    private static DbType MapDbType(string? dbType)
    {
        return DataSourceDriverRegistry.ResolveDbType(dbType);
    }
}
