using Atlas.Core.DataSource;
using Atlas.Core.Models;
using Atlas.Core.Plugins;

namespace Atlas.Infrastructure.DataSource;

/// <summary>
/// SQLite 内置数据源连接器
/// </summary>
public sealed class SqliteDataSourceConnector : IDataSourceConnector
{
    public string Code => "datasource.sqlite";
    public string Name => "SQLite";
    public string Version => "1.0.0";
    public string Description => "内置 SQLite 数据源连接器";
    public string DataSourceType => "sqlite";
    public PluginCategory Category => PluginCategory.DataSource;

    public Task OnLoadedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task OnUnloadingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            conn.Open();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<DataSourceSchema> GetSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        var tables = new List<TableInfo>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var tableNames = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            tableNames.Add(reader.GetString(0));
        }

        foreach (var tableName in tableNames)
        {
            var columns = await GetColumnsAsync(conn, tableName, cancellationToken);
            tables.Add(new TableInfo(tableName, null, columns));
        }

        return new DataSourceSchema(tables, []);
    }

    public async Task<PagedResult<Dictionary<string, object?>>> QueryAsync(
        string connectionString,
        string sql,
        Dictionary<string, object?> parameters,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        var countSql = $"SELECT COUNT(*) FROM ({sql}) __count";
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = countSql;
        AddParameters(countCmd, parameters);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));

        var pagedSql = $"{sql} LIMIT {pageSize} OFFSET {(pageIndex - 1) * pageSize}";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = pagedSql;
        AddParameters(cmd, parameters);

        var items = new List<Dictionary<string, object?>>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            items.Add(row);
        }

        return new PagedResult<Dictionary<string, object?>>(items, total, pageIndex, pageSize);
    }

    public async Task<int> ExecuteAsync(
        string connectionString,
        string sql,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        string tableName,
        CancellationToken cancellationToken)
    {
        using var cmd = conn.CreateCommand();
        // 使用双引号引用表名，并将内部双引号转义为 ""，防止含特殊字符的表名引发意外行为
        var quotedName = tableName.Replace("\"", "\"\"");
        cmd.CommandText = $"PRAGMA table_info(\"{quotedName}\")";
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var columns = new List<ColumnInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new ColumnInfo(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3) == 0,
                reader.GetInt32(5) > 0,
                null));
        }

        return columns;
    }

    private static void AddParameters(Microsoft.Data.Sqlite.SqliteCommand cmd, Dictionary<string, object?> parameters)
    {
        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k.StartsWith('@') ? k : $"@{k}", v ?? DBNull.Value);
        }
    }
}
