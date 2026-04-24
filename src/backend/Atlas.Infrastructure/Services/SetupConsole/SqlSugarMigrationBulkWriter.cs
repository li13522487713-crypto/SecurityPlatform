using System.Data;
using System.Reflection;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

public sealed class SqlSugarMigrationBulkWriter : IMigrationBulkWriter
{
    private readonly ILogger<SqlSugarMigrationBulkWriter> _logger;

    public SqlSugarMigrationBulkWriter(ILogger<SqlSugarMigrationBulkWriter> logger)
    {
        _logger = logger;
    }

    public async Task<long> PrepareTargetAsync(
        DataMigrationPlanItem item,
        ResolvedMigrationConnection source,
        ResolvedMigrationConnection target,
        string writeMode,
        bool createSchema,
        CancellationToken cancellationToken = default)
    {
        using var targetScope = MigrationSqlSugarScopeFactory.Create(target.ConnectionString, target.DbType);
        if (createSchema)
        {
            await EnsureTargetSchemaAsync(targetScope, target.DbType, item, cancellationToken).ConfigureAwait(false);
        }

        var rowsBefore = await CountTargetRowsAsync(item, target, cancellationToken).ConfigureAwait(false);
        if (string.Equals(writeMode, DataMigrationWriteModes.TruncateThenInsert, StringComparison.OrdinalIgnoreCase)
            && rowsBefore > 0)
        {
            await TruncateOrDeleteAsync(targetScope, target.DbType, item, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        return rowsBefore;
    }

    public async Task<MigrationBatchWriteResult> WriteNextBatchAsync(
        DataMigrationPlanItem item,
        ResolvedMigrationConnection source,
        ResolvedMigrationConnection target,
        string writeMode,
        int batchSize,
        long lastMaxId,
        CancellationToken cancellationToken = default)
    {
        using var sourceScope = MigrationSqlSugarScopeFactory.Create(source.ConnectionString, source.DbType);
        using var targetScope = MigrationSqlSugarScopeFactory.Create(target.ConnectionString, target.DbType);

        if (item.Kind == "entity" && item.EntityType is not null)
        {
            var rows = QueryEntityBatch(sourceScope, item.EntityType, item.KeyColumn, lastMaxId, batchSize);
            if (rows.Count == 0)
            {
                return new MigrationBatchWriteResult(false, 0, lastMaxId, true);
            }

            var nextLastMaxId = ExtractLastEntityId(rows[^1], item.KeyColumn);
            var bulkWarning = await TryBulkCopyEntityBatchAsync(targetScope, item.EntityType, rows).ConfigureAwait(false);
            if (bulkWarning is not null)
            {
                await InsertEntityBatchAsync(targetScope, item.EntityType, rows, writeMode).ConfigureAwait(false);
            }

            return new MigrationBatchWriteResult(
                true,
                rows.Count,
                nextLastMaxId,
                bulkWarning is null,
                bulkWarning);
        }

        var dataTable = QueryRawTableBatch(sourceScope, source.DbType, item.TableName, item.KeyColumn, lastMaxId, batchSize);
        if (dataTable.Rows.Count == 0)
        {
            return new MigrationBatchWriteResult(false, 0, lastMaxId, true);
        }

        var rawNextLastMaxId = Convert.ToInt64(dataTable.Rows[dataTable.Rows.Count - 1][item.KeyColumn]);
        var rawBulkWarning = await TryBulkCopyDataTableAsync(targetScope, item.TableName, dataTable).ConfigureAwait(false);
        if (rawBulkWarning is not null)
        {
            await InsertDataTableBatchAsync(targetScope, target.DbType, item.TableName, dataTable, cancellationToken).ConfigureAwait(false);
        }

        return new MigrationBatchWriteResult(
            true,
            dataTable.Rows.Count,
            rawNextLastMaxId,
            rawBulkWarning is null,
            rawBulkWarning);
    }

    public async Task<long> CountTargetRowsAsync(
        DataMigrationPlanItem item,
        ResolvedMigrationConnection target,
        CancellationToken cancellationToken = default)
    {
        using var targetScope = MigrationSqlSugarScopeFactory.Create(target.ConnectionString, target.DbType);
        if (item.Kind == "entity" && item.EntityType is not null)
        {
            return CountEntity(targetScope, item.EntityType);
        }

        if (!targetScope.DbMaintenance.IsAnyTable(item.TableName, false))
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await targetScope.Ado.GetScalarAsync(
            MigrationSqlSugarScopeFactory.BuildCountSql(target.DbType, item.TableName)).ConfigureAwait(false);
        return Convert.ToInt64(result ?? 0);
    }

    private async Task EnsureTargetSchemaAsync(
        ISqlSugarClient targetScope,
        string targetDbType,
        DataMigrationPlanItem item,
        CancellationToken cancellationToken)
    {
        if (item.Kind == "entity" && item.EntityType is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            targetScope.CodeFirst.InitTables(item.EntityType);
            return;
        }

        if (targetScope.DbMaintenance.IsAnyTable(item.TableName, false))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await targetScope.Ado.ExecuteCommandAsync(
            MigrationSqlSugarScopeFactory.BuildCreateAiTableSql(targetDbType, item.TableName)).ConfigureAwait(false);
        foreach (var columnName in new[]
                 {
                     MigrationSqlSugarScopeFactory.AiOwnerUserIdColumn,
                     MigrationSqlSugarScopeFactory.AiChannelIdColumn
                 })
        {
            await targetScope.Ado.ExecuteCommandAsync(
                MigrationSqlSugarScopeFactory.BuildCreateAiIndexSql(targetDbType, item.TableName, columnName, columnName))
                .ConfigureAwait(false);
        }
    }

    private async Task TruncateOrDeleteAsync(
        ISqlSugarClient targetScope,
        string targetDbType,
        DataMigrationPlanItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (item.Kind == "entity")
            {
                targetScope.DbMaintenance.TruncateTable(item.TableName);
                return;
            }

            await targetScope.Ado.ExecuteCommandAsync(
                MigrationSqlSugarScopeFactory.BuildTruncateSql(targetDbType, item.TableName)).ConfigureAwait(false);
        }
        catch
        {
            await targetScope.Ado.ExecuteCommandAsync(
                MigrationSqlSugarScopeFactory.BuildDeleteAllSql(targetDbType, item.TableName)).ConfigureAwait(false);
        }
    }

    private async Task<string?> TryBulkCopyEntityBatchAsync(
        ISqlSugarClient targetScope,
        Type entityType,
        IReadOnlyList<object> rows)
    {
        try
        {
            var fastestMethod = targetScope.GetType().GetMethods()
                .First(method => method.Name == "Fastest" && method.IsGenericMethod && method.GetParameters().Length == 0);
            var fastest = fastestMethod.MakeGenericMethod(entityType).Invoke(targetScope, null)
                ?? throw new InvalidOperationException($"Fastest<{entityType.Name}>() returned null.");
            var bulkMethod = fastest.GetType().GetMethods()
                .First(method => method.Name == "BulkCopy" && method.GetParameters().Length == 1);
            var typedList = CastObjectList(entityType, rows);
            bulkMethod.Invoke(fastest, new[] { typedList });
            return null;
        }
        catch (Exception ex)
        {
            var message = $"BulkCopy not supported for entity {entityType.Name}: {Unwrap(ex).Message}";
            _logger.LogWarning(ex, message);
            return message;
        }
    }

    private static Task InsertEntityBatchAsync(
        ISqlSugarClient targetScope,
        Type entityType,
        IReadOnlyList<object> rows,
        string _writeMode)
    {
        var typedArray = Array.CreateInstance(entityType, rows.Count);
        for (var index = 0; index < rows.Count; index += 1)
        {
            typedArray.SetValue(rows[index], index);
        }

        var insertableMethod = targetScope.GetType().GetMethods()
            .First(method => method.Name == "Insertable"
                             && method.IsGenericMethod
                             && method.GetParameters().Length == 1
                             && method.GetParameters()[0].ParameterType.IsArray);
        var insertable = insertableMethod.MakeGenericMethod(entityType).Invoke(targetScope, new object[] { typedArray })
            ?? throw new InvalidOperationException($"Insertable<{entityType.Name}>() returned null.");
        var execMethod = insertable.GetType().GetMethods()
            .First(method => method.Name == "ExecuteCommand" && method.GetParameters().Length == 0);
        execMethod.Invoke(insertable, null);
        return Task.CompletedTask;
    }

    private async Task<string?> TryBulkCopyDataTableAsync(
        ISqlSugarClient targetScope,
        string tableName,
        DataTable dataTable)
    {
        try
        {
            var fastestMethod = targetScope.GetType().GetMethods()
                .First(method => method.Name == "Fastest" && method.IsGenericMethod && method.GetParameters().Length == 0);
            var fastest = fastestMethod.MakeGenericMethod(typeof(DataTable)).Invoke(targetScope, null)
                ?? throw new InvalidOperationException("Fastest<DataTable>() returned null.");
            var asMethod = fastest.GetType().GetMethods()
                .First(method => method.Name == "AS" && method.GetParameters().Length == 1);
            var targetFastest = asMethod.Invoke(fastest, new object[] { tableName }) ?? fastest;
            var bulkMethod = targetFastest.GetType().GetMethods()
                .First(method => method.Name == "BulkCopy" && method.GetParameters().Length == 1);
            bulkMethod.Invoke(targetFastest, new object[] { dataTable });
            return null;
        }
        catch (Exception ex)
        {
            var message = $"BulkCopy not supported for table {tableName}: {Unwrap(ex).Message}";
            _logger.LogWarning(ex, message);
            return message;
        }
    }

    private static async Task InsertDataTableBatchAsync(
        ISqlSugarClient targetScope,
        string targetDbType,
        string tableName,
        DataTable table,
        CancellationToken cancellationToken)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        var columns = table.Columns.Cast<DataColumn>().ToArray();
        var quotedColumns = columns
            .Select(column => MigrationSqlSugarScopeFactory.QuoteIdentifier(targetDbType, column.ColumnName))
            .ToArray();
        var parameters = new List<SugarParameter>();
        string sql;

        if (string.Equals(DataSourceDriverRegistry.NormalizeDriverCode(targetDbType), "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            var segments = new List<string>();
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex += 1)
            {
                var values = new List<string>();
                for (var columnIndex = 0; columnIndex < columns.Length; columnIndex += 1)
                {
                    var parameterName = $"@p_{rowIndex}_{columnIndex}";
                    values.Add(parameterName);
                    parameters.Add(new SugarParameter(parameterName, table.Rows[rowIndex][columnIndex] ?? DBNull.Value));
                }

                segments.Add(
                    $"INTO {MigrationSqlSugarScopeFactory.QuoteIdentifier(targetDbType, tableName)} ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", values)})");
            }

            sql = $"INSERT ALL {string.Join(" ", segments)} SELECT 1 FROM DUAL";
        }
        else
        {
            var valueSegments = new List<string>();
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex += 1)
            {
                var rowValues = new List<string>();
                for (var columnIndex = 0; columnIndex < columns.Length; columnIndex += 1)
                {
                    var parameterName = $"@p_{rowIndex}_{columnIndex}";
                    rowValues.Add(parameterName);
                    parameters.Add(new SugarParameter(parameterName, table.Rows[rowIndex][columnIndex] ?? DBNull.Value));
                }

                valueSegments.Add($"({string.Join(", ", rowValues)})");
            }

            sql =
                $"INSERT INTO {MigrationSqlSugarScopeFactory.QuoteIdentifier(targetDbType, tableName)} ({string.Join(", ", quotedColumns)}) VALUES {string.Join(", ", valueSegments)};";
        }

        cancellationToken.ThrowIfCancellationRequested();
        await targetScope.Ado.ExecuteCommandAsync(sql, parameters.ToArray()).ConfigureAwait(false);
    }

    private static List<object> QueryEntityBatch(
        ISqlSugarClient sourceScope,
        Type entityType,
        string keyPropertyName,
        long lastMaxId,
        int batchSize)
    {
        var queryableMethod = sourceScope.GetType().GetMethods()
            .First(method => method.Name == "Queryable" && method.IsGenericMethod && method.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(sourceScope, null)
            ?? throw new InvalidOperationException($"Queryable<{entityType.Name}>() returned null.");

        queryable = InvokeBestMatch(queryable, "Where", $"{keyPropertyName} > @lastMaxId", new { lastMaxId });
        queryable = InvokeBestMatch(queryable, "OrderBy", $"{keyPropertyName} asc");
        queryable = InvokeBestMatch(queryable, "Take", batchSize);
        var toListMethod = queryable.GetType().GetMethods()
            .First(method => method.Name == "ToList" && method.GetParameters().Length == 0);
        return ((System.Collections.IList?)toListMethod.Invoke(queryable, null))?.Cast<object>().ToList() ?? [];
    }

    private static DataTable QueryRawTableBatch(
        ISqlSugarClient sourceScope,
        string sourceDbType,
        string tableName,
        string keyColumn,
        long lastMaxId,
        int batchSize)
    {
        var sql = MigrationSqlSugarScopeFactory.BuildPagedSelectSql(sourceDbType, tableName, keyColumn, batchSize);
        return sourceScope.Ado.GetDataTable(sql, new SugarParameter("@lastMaxId", lastMaxId));
    }

    private static object CastObjectList(Type entityType, IReadOnlyList<object> rows)
    {
        var listType = typeof(List<>).MakeGenericType(entityType);
        var list = Activator.CreateInstance(listType)
            ?? throw new InvalidOperationException($"Failed to create List<{entityType.Name}>.");
        var addMethod = listType.GetMethod("Add")
            ?? throw new InvalidOperationException($"List<{entityType.Name}>.Add missing.");
        foreach (var row in rows)
        {
            addMethod.Invoke(list, new[] { row });
        }

        return list;
    }

    private static long ExtractLastEntityId(object entity, string keyPropertyName)
    {
        var property = entity.GetType().GetProperty(keyPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new InvalidOperationException($"Primary key property {keyPropertyName} not found on {entity.GetType().Name}.");
        var value = property.GetValue(entity);
        return Convert.ToInt64(value ?? 0);
    }

    private static long CountEntity(ISqlSugarClient scope, Type entityType)
    {
        var queryableMethod = scope.GetType().GetMethods()
            .First(method => method.Name == "Queryable" && method.IsGenericMethod && method.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null)
            ?? throw new InvalidOperationException($"Queryable<{entityType.Name}>() returned null.");
        var countMethod = queryable.GetType().GetMethods()
            .First(method => method.Name == "Count" && method.GetParameters().Length == 0);
        return Convert.ToInt64(countMethod.Invoke(queryable, null) ?? 0);
    }

    private static object InvokeBestMatch(object target, string methodName, params object[] args)
    {
        var candidates = target.GetType().GetMethods()
            .Where(method => method.Name == methodName && method.GetParameters().Length == args.Length)
            .ToArray();
        foreach (var candidate in candidates)
        {
            try
            {
                return candidate.Invoke(target, args) ?? target;
            }
            catch
            {
                // Try next overload.
            }
        }

        throw new InvalidOperationException($"Method {methodName} with {args.Length} arguments not found on {target.GetType().Name}.");
    }

    private static Exception Unwrap(Exception ex)
        => ex is TargetInvocationException { InnerException: not null } tie ? tie.InnerException : ex;
}
