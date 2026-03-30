using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// SQLite 列可空/缺列漂移检测与按 ORM 重建回灌，供主库初始化与应用迁移共用。
/// </summary>
internal static class SqliteSchemaAlignment
{
    public static bool IsSqlite(ISqlSugarClient db) =>
        db.CurrentConnectionConfig?.DbType == DbType.Sqlite;

    public static bool RequiresNullableColumnFix<TEntity>(ISqlSugarClient db, params string[] columnNames)
        where TEntity : class, new()
    {
        if (columnNames.Length == 0)
        {
            return false;
        }

        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return false;
        }

        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        foreach (var columnName in columnNames)
        {
            var column = columns.FirstOrDefault(c =>
                string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (column is not null && !column.IsNullable)
            {
                return true;
            }
        }

        return false;
    }

    public static bool RequiresMissingColumnFix<TEntity>(ISqlSugarClient db, params string[] requiredColumnNames)
        where TEntity : class, new()
    {
        if (requiredColumnNames.Length == 0)
        {
            return false;
        }

        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return false;
        }

        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        foreach (var columnName in requiredColumnNames)
        {
            var hasColumn = columns.Any(c =>
                string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (!hasColumn)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task RebuildTableViaOrmAsync<TEntity>(ISqlSugarClient db, CancellationToken cancellationToken)
        where TEntity : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var data = await db.Queryable<TEntity>().ToListAsync(cancellationToken);
        db.DbMaintenance.DropTable(tableName);
        db.CodeFirst.InitTables<TEntity>();
        if (data.Count > 0)
        {
            await db.Insertable(data).ExecuteCommandAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 应用域成员/组织相关表：按需检测可空列漂移或缺列后整表重建并回灌。
    /// </summary>
    public static async Task<SchemaAlignmentReport> EnsureAppMembershipDomainSchemaAsync(
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        if (!IsSqlite(db))
        {
            return new SchemaAlignmentReport(Array.Empty<string>(), messages);
        }

        var repaired = new List<string>();

        async Task TryRebuildAsync<T>(string logicalName, Func<bool> needsFix, string reason)
            where T : class, new()
        {
            if (!needsFix())
            {
                return;
            }

            var table = db.EntityMaintenance.GetTableName<T>();
            await RebuildTableViaOrmAsync<T>(db, cancellationToken);
            repaired.Add(table);
            messages.Add($"{logicalName}({table}): {reason}");
        }

        await TryRebuildAsync<AppRole>(
            nameof(AppRole),
            () =>
                RequiresNullableColumnFix<AppRole>(db, "DeptIds")
                || RequiresMissingColumnFix<AppRole>(db, "DeptIds", "DataScope"),
            "rebuild nullable/missing DeptIds|DataScope");

        await TryRebuildAsync<AppDepartment>(
            nameof(AppDepartment),
            () =>
                RequiresNullableColumnFix<AppDepartment>(db, "ParentId")
                || RequiresMissingColumnFix<AppDepartment>(db, "ParentId"),
            "rebuild nullable/missing ParentId");

        await TryRebuildAsync<AppPermission>(
            nameof(AppPermission),
            () =>
                RequiresNullableColumnFix<AppPermission>(db, "Description")
                || RequiresMissingColumnFix<AppPermission>(db, "Description"),
            "rebuild nullable/missing Description");

        await TryRebuildAsync<AppPosition>(
            nameof(AppPosition),
            () =>
                RequiresNullableColumnFix<AppPosition>(db, "Description")
                || RequiresMissingColumnFix<AppPosition>(db, "Description"),
            "rebuild nullable/missing Description");

        await TryRebuildAsync<AppProject>(
            nameof(AppProject),
            () =>
                RequiresNullableColumnFix<AppProject>(db, "Description")
                || RequiresMissingColumnFix<AppProject>(db, "Description"),
            "rebuild nullable/missing Description");

        if (messages.Count == 0)
        {
            messages.Add("应用域表结构无需重建（未检测到可空/缺列漂移）。");
        }

        return new SchemaAlignmentReport(repaired, messages);
    }
}

internal sealed record SchemaAlignmentReport(
    IReadOnlyList<string> RepairedTables,
    IReadOnlyList<string> Messages);
