using Atlas.Core.DataSource;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Plugins;
using SqlSugar;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.DataSource;

/// <summary>
/// SQLite 内置数据源连接器
/// </summary>
public sealed class SqliteDataSourceConnector : IDataSourceConnector
{
    // 禁止的 SQL 关键字：写操作与 DDL，防止非 SELECT 语句被执行
    private static readonly Regex DangerousStatementPattern = new(
        @"(^\s*|;\s*)(INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|TRUNCATE|REPLACE|ATTACH|DETACH|PRAGMA|VACUUM)\s",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 多语句分隔符检测（分号后跟非空内容视为多语句）
    private static readonly Regex MultiStatementPattern = new(
        @";\s*\S",
        RegexOptions.Compiled);

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
            using var client = CreateClient(connectionString);
            _ = client.DbMaintenance.GetTableInfoList(false);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<DataSourceSchema> GetSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        using var client = CreateClient(connectionString);
        var tables = new List<TableInfo>();
        var tableInfos = client.DbMaintenance.GetTableInfoList(false);
        foreach (var tableInfo in tableInfos.OrderBy(x => x.Name))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var columns = await GetColumnsAsync(client, tableInfo.Name, cancellationToken);
            tables.Add(new TableInfo(tableInfo.Name, null, columns));
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
        ValidateReadOnlySql(sql);

        if (pageIndex < 1)
            throw new BusinessException($"pageIndex 必须 >= 1，当前值：{pageIndex}", "INVALID_PAGE_INDEX");
        if (pageSize < 1)
            throw new BusinessException($"pageSize 必须 >= 1，当前值：{pageSize}", "INVALID_PAGE_SIZE");

        using var client = CreateClient(connectionString);
        var sugarParams = parameters
            .Select(kv => new SugarParameter(kv.Key.StartsWith('@') ? kv.Key : $"@{kv.Key}", kv.Value ?? DBNull.Value))
            .ToArray();
        var query = client.SqlQueryable<Dictionary<string, object?>>(sql);
        if (sugarParams.Length > 0)
        {
            query = query.AddParameters(sugarParams);
        }

        RefAsync<int> totalRef = 0;
        var rows = await query.ToPageListAsync(pageIndex, pageSize, totalRef);
        var items = rows
            .Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var total = totalRef.Value;

        return new PagedResult<Dictionary<string, object?>>(items, total, pageIndex, pageSize);
    }

    public async Task<int> ExecuteAsync(
        string connectionString,
        string sql,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(connectionString);
        var sugarParams = parameters
            .Select(kv => new SugarParameter(kv.Key.StartsWith('@') ? kv.Key : $"@{kv.Key}", kv.Value ?? DBNull.Value))
            .ToArray();
        return await client.Ado.ExecuteCommandAsync(sql, sugarParams);
    }

    /// <summary>
    /// 校验 SQL 是否为纯只读查询（仅允许 SELECT）。
    /// 阻止多语句、DDL 及写操作，防止通过数据源连接器执行破坏性指令。
    /// </summary>
    private static void ValidateReadOnlySql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new BusinessException("查询语句不能为空", "SQL_EMPTY");

        var trimmed = sql.Trim();

        // 必须以 SELECT（包括注释清理后的有效起始）开头
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("数据源查询仅允许 SELECT 或 CTE（WITH...SELECT）语句", "SQL_NOT_READONLY");
        }

        // 禁止危险语句关键字
        if (DangerousStatementPattern.IsMatch(sql))
            throw new BusinessException("查询语句包含不允许的操作（INSERT/UPDATE/DELETE/DDL/PRAGMA 等）", "SQL_FORBIDDEN_STATEMENT");

        // 禁止多语句（分号后跟有效内容）
        if (MultiStatementPattern.IsMatch(sql))
            throw new BusinessException("查询语句不允许包含多条语句（分号分隔）", "SQL_MULTI_STATEMENT");
    }

    private static Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(
        SqlSugarClient client,
        string tableName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var columns = client.DbMaintenance.GetColumnInfosByTableName(tableName, false)
            .Select(x => new ColumnInfo(
                x.DbColumnName,
                x.DataType,
                x.IsNullable,
                x.IsPrimarykey,
                null))
            .ToArray();
        return Task.FromResult<IReadOnlyList<ColumnInfo>>(columns);
    }

    private static SqlSugarClient CreateClient(string connectionString)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }
}
