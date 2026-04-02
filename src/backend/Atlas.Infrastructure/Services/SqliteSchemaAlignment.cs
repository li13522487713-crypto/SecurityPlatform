using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// SQLite 列可空/缺列漂移检测与按 ORM 重建回灌，供主库初始化与应用迁移共用。
/// </summary>
internal static class SqliteSchemaAlignment
{
    private const string EmptyTenantId = "00000000-0000-0000-0000-000000000000";

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

    public static SqliteCatalogCleanupResult CleanupBrokenSchemaEntries(ISqlSugarClient db)
    {
        try
        {
            db.Ado.ExecuteCommand("PRAGMA writable_schema=ON;");
            var invalidTypeRows = db.Ado.ExecuteCommand(
                """
                DELETE FROM sqlite_master
                WHERE lower(type) NOT IN ('table', 'index', 'view', 'trigger');
                """);
            var httpRouteRows = db.Ado.ExecuteCommand(
                """
                DELETE FROM sqlite_master
                WHERE name GLOB 'GET *'
                   OR name GLOB 'POST *'
                   OR name GLOB 'PUT *'
                   OR name GLOB 'PATCH *'
                   OR name GLOB 'DELETE *'
                   OR name GLOB 'OPTIONS *'
                   OR tbl_name GLOB 'GET *'
                   OR tbl_name GLOB 'POST *'
                   OR tbl_name GLOB 'PUT *'
                   OR tbl_name GLOB 'PATCH *'
                   OR tbl_name GLOB 'DELETE *'
                   OR tbl_name GLOB 'OPTIONS *';
                """);
            var duplicateRows = db.Ado.ExecuteCommand(
                """
                DELETE FROM sqlite_master
                WHERE rowid IN (
                    SELECT m1.rowid
                    FROM sqlite_master m1
                    WHERE EXISTS (
                        SELECT 1
                        FROM sqlite_master m2
                        WHERE m2.type = m1.type
                          AND m2.name = m1.name
                          AND m2.rowid < m1.rowid
                    )
                );
                """);

            return new SqliteCatalogCleanupResult(invalidTypeRows, httpRouteRows, duplicateRows);
        }
        finally
        {
            db.Ado.ExecuteCommand("PRAGMA writable_schema=OFF;");
        }
    }

    private static async Task RebuildAppDepartmentViaSqlAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        await RebuildTableViaSqlCopyAsync<AppDepartment>(
            db,
            BuildAppDepartmentCopySql,
            cancellationToken);
    }

    private static async Task RebuildAppPermissionViaSqlAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        await RebuildTableViaSqlCopyAsync<AppPermission>(
            db,
            BuildAppPermissionCopySql,
            cancellationToken);
    }

    private static async Task RebuildTableViaSqlCopyAsync<TEntity>(
        ISqlSugarClient db,
        Func<string, string, string> copySqlFactory,
        CancellationToken cancellationToken)
        where TEntity : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var backupTableName = await PrepareBackupTableAsync(db, tableName, cancellationToken);
        db.CodeFirst.InitTables<TEntity>();
        cancellationToken.ThrowIfCancellationRequested();
        var copySql = copySqlFactory(tableName, backupTableName);
        await db.Ado.ExecuteCommandAsync(copySql);
        cancellationToken.ThrowIfCancellationRequested();
        await db.Ado.ExecuteCommandAsync($"DROP TABLE \"{backupTableName}\"");
    }

    private static async Task<string> PrepareBackupTableAsync(
        ISqlSugarClient db,
        string tableName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backupTableName = $"{tableName}__bak_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        CleanupBrokenSchemaEntries(db);
        if (db.DbMaintenance.IsAnyTable(backupTableName, false))
        {
            db.DbMaintenance.DropTable(backupTableName);
        }

        try
        {
            await db.Ado.ExecuteCommandAsync(
                $"ALTER TABLE \"{tableName}\" RENAME TO \"{backupTableName}\"");
        }
        catch (Exception ex) when (IsRenameConflictedByBrokenSchema(ex))
        {
            CleanupBrokenSchemaEntries(db);
            if (db.DbMaintenance.IsAnyTable(backupTableName, false))
            {
                db.DbMaintenance.DropTable(backupTableName);
            }

            await db.Ado.ExecuteCommandAsync(
                $"ALTER TABLE \"{tableName}\" RENAME TO \"{backupTableName}\"");
        }

        if (!db.DbMaintenance.IsAnyTable(backupTableName, false))
        {
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
                throw new InvalidOperationException($"Failed to prepare backup table for {tableName}.");
            }

            await db.Ado.ExecuteCommandAsync(
                $"CREATE TABLE \"{backupTableName}\" AS SELECT * FROM \"{tableName}\";");
            db.DbMaintenance.DropTable(tableName);
        }

        return backupTableName;
    }

    private static string BuildAppDepartmentCopySql(string tableName, string backupTableName)
    {
        // 历史库可能将 ParentId 或 TenantIdValue 保存为脏值，这里做 SQL 级清洗后回灌。
        return
            $"""
            INSERT INTO "{tableName}" ("Id","TenantIdValue","AppId","Name","Code","ParentId","SortOrder")
            SELECT
                CAST("Id" AS INTEGER) AS "Id",
                CASE
                    WHEN "TenantIdValue" IS NULL THEN '{EmptyTenantId}'
                    WHEN TRIM(CAST("TenantIdValue" AS TEXT)) = '' THEN '{EmptyTenantId}'
                    WHEN LOWER(TRIM(CAST("TenantIdValue" AS TEXT))) LIKE '________-____-____-____-____________'
                        THEN TRIM(CAST("TenantIdValue" AS TEXT))
                    ELSE '{EmptyTenantId}'
                END AS "TenantIdValue",
                COALESCE(CAST("AppId" AS INTEGER), 0) AS "AppId",
                COALESCE(NULLIF(TRIM(CAST("Name" AS TEXT)), ''), 'Legacy Department') AS "Name",
                COALESCE(NULLIF(TRIM(CAST("Code" AS TEXT)), ''), 'legacy_department_' || CAST("Id" AS TEXT)) AS "Code",
                COALESCE(
                    CASE
                        WHEN "ParentId" IS NULL THEN NULL
                        WHEN TRIM(CAST("ParentId" AS TEXT)) = '' THEN NULL
                        WHEN TRIM(CAST("ParentId" AS TEXT)) GLOB '[0-9]*' THEN CAST("ParentId" AS INTEGER)
                        WHEN TRIM(CAST("ParentId" AS TEXT)) GLOB '-[0-9]*' THEN CAST("ParentId" AS INTEGER)
                        ELSE NULL
                    END,
                    CAST("Id" AS INTEGER)
                ) AS "ParentId",
                COALESCE(CAST("SortOrder" AS INTEGER), 0) AS "SortOrder"
            FROM "{backupTableName}";
            """;
    }

    private static string BuildAppPermissionCopySql(string tableName, string backupTableName)
    {
        // 历史库可能将 TenantIdValue 写成非 GUID 文本；回灌时做兜底清洗避免 ORM 绑定失败。
        return
            $"""
            INSERT INTO "{tableName}" ("Id","TenantIdValue","AppId","Name","Code","Type","Description")
            SELECT
                CAST("Id" AS INTEGER) AS "Id",
                CASE
                    WHEN "TenantIdValue" IS NULL THEN '{EmptyTenantId}'
                    WHEN TRIM(CAST("TenantIdValue" AS TEXT)) = '' THEN '{EmptyTenantId}'
                    WHEN LOWER(TRIM(CAST("TenantIdValue" AS TEXT))) LIKE '________-____-____-____-____________'
                        THEN TRIM(CAST("TenantIdValue" AS TEXT))
                    ELSE '{EmptyTenantId}'
                END AS "TenantIdValue",
                COALESCE(CAST("AppId" AS INTEGER), 0) AS "AppId",
                COALESCE(NULLIF(TRIM(CAST("Name" AS TEXT)), ''), 'Legacy Permission') AS "Name",
                COALESCE(NULLIF(TRIM(CAST("Code" AS TEXT)), ''), 'legacy_permission_' || CAST("Id" AS TEXT)) AS "Code",
                COALESCE(NULLIF(TRIM(CAST("Type" AS TEXT)), ''), 'Api') AS "Type",
                COALESCE(CAST("Description" AS TEXT), '') AS "Description"
            FROM "{backupTableName}";
            """;
    }

    private static bool HasInvalidTenantIdValue<TEntity>(ISqlSugarClient db)
        where TEntity : class, new()
    {
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return false;
        }

        var hasTenantIdColumn = db.DbMaintenance.GetColumnInfosByTableName(tableName, false)
            .Any(c => string.Equals(c.DbColumnName, "TenantIdValue", StringComparison.OrdinalIgnoreCase));
        if (!hasTenantIdColumn)
        {
            return false;
        }

        var invalidCount = db.Ado.GetInt(
            $"""
             SELECT COUNT(1)
             FROM "{tableName}"
             WHERE "TenantIdValue" IS NULL
                OR TRIM(CAST("TenantIdValue" AS TEXT)) = ''
                OR LOWER(TRIM(CAST("TenantIdValue" AS TEXT))) NOT LIKE '________-____-____-____-____________';
             """);
        return invalidCount > 0;
    }

    private static bool IsRenameConflictedByBrokenSchema(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("after rename", StringComparison.OrdinalIgnoreCase)
               && message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
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
        var cleanupResult = CleanupBrokenSchemaEntries(db);
        if (cleanupResult.TotalRemoved > 0)
        {
            messages.Add(
                $"sqlite_master auto-clean: invalidType={cleanupResult.InvalidTypeEntriesRemoved}, " +
                $"httpRoute={cleanupResult.HttpRouteEntriesRemoved}, duplicate={cleanupResult.DuplicateEntriesRemoved}");
        }

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

        if (RequiresNullableColumnFix<AppDepartment>(db, "ParentId")
            || RequiresMissingColumnFix<AppDepartment>(db, "ParentId"))
        {
            var table = db.EntityMaintenance.GetTableName<AppDepartment>();
            await RebuildAppDepartmentViaSqlAsync(db, cancellationToken);
            repaired.Add(table);
            messages.Add($"{nameof(AppDepartment)}({table}): rebuild nullable/missing ParentId via SQL-safe cast");
        }

        if (RequiresNullableColumnFix<AppPermission>(db, "Description")
            || RequiresMissingColumnFix<AppPermission>(db, "Description")
            || HasInvalidTenantIdValue<AppPermission>(db))
        {
            var table = db.EntityMaintenance.GetTableName<AppPermission>();
            await RebuildAppPermissionViaSqlAsync(db, cancellationToken);
            repaired.Add(table);
            messages.Add($"{nameof(AppPermission)}({table}): rebuild nullable/missing Description via SQL-safe cast");
        }

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

internal sealed record SqliteCatalogCleanupResult(
    int InvalidTypeEntriesRemoved,
    int HttpRouteEntriesRemoved,
    int DuplicateEntriesRemoved)
{
    public int TotalRemoved => InvalidTypeEntriesRemoved + HttpRouteEntriesRemoved + DuplicateEntriesRemoved;
}
