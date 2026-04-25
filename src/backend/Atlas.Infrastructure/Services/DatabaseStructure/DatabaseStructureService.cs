using System.Data;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed class DatabaseStructureService : IDatabaseStructureService
{
    private const int QueryTimeoutSeconds = 10;

    private readonly IAiDatabaseClientFactory _clientFactory;
    private readonly IDatabaseDialectRegistry _dialects;
    private readonly ISqlSafetyValidator _sqlSafetyValidator;
    private readonly ILogger<DatabaseStructureService> _logger;

    public DatabaseStructureService(
        IAiDatabaseClientFactory clientFactory,
        IDatabaseDialectRegistry dialects,
        ISqlSafetyValidator sqlSafetyValidator,
        ILogger<DatabaseStructureService> logger)
    {
        _clientFactory = clientFactory;
        _dialects = dialects;
        _sqlSafetyValidator = sqlSafetyValidator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DatabaseObjectDto>> GetObjectsAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string type,
        CancellationToken cancellationToken)
    {
        using var scope = await OpenAsync(tenantId, databaseId, environment, cancellationToken);
        var sql = scope.Dialect.BuildListObjectsSql(type);
        var table = await scope.Client.Ado.GetDataTableAsync(sql);
        return table.Rows.Cast<DataRow>().Select(MapObject).Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
    }

    public async Task<IReadOnlyList<DatabaseColumnDto>> GetTableColumnsAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string tableName,
        string? schema,
        CancellationToken cancellationToken)
        => await GetColumnsAsync(tenantId, databaseId, environment, tableName, schema, cancellationToken);

    public async Task<IReadOnlyList<DatabaseColumnDto>> GetViewColumnsAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string viewName,
        string? schema,
        CancellationToken cancellationToken)
        => await GetColumnsAsync(tenantId, databaseId, environment, viewName, schema, cancellationToken);

    public async Task<DdlResponse> GetTableDdlAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string tableName,
        string? schema,
        CancellationToken cancellationToken)
        => await GetDdlAsync(tenantId, databaseId, environment, tableName, schema, "table", cancellationToken);

    public async Task<DdlResponse> GetViewDdlAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string viewName,
        string? schema,
        CancellationToken cancellationToken)
        => await GetDdlAsync(tenantId, databaseId, environment, viewName, schema, "view", cancellationToken);

    public Task<PreviewDataResponse> PreviewTableDataAsync(
        TenantId tenantId,
        long databaseId,
        string tableName,
        PreviewDataRequest request,
        CancellationToken cancellationToken)
        => PreviewObjectDataAsync(tenantId, databaseId, tableName, request, cancellationToken);

    public Task<PreviewDataResponse> PreviewViewDataAsync(
        TenantId tenantId,
        long databaseId,
        string viewName,
        PreviewDataRequest request,
        CancellationToken cancellationToken)
        => PreviewObjectDataAsync(tenantId, databaseId, viewName, request, cancellationToken);

    public async Task<DdlResponse> BuildCreateTableDdlAsync(
        TenantId tenantId,
        long databaseId,
        PreviewCreateTableDdlRequest request,
        CancellationToken cancellationToken)
    {
        using var scope = await OpenAsync(tenantId, databaseId, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        return new DdlResponse(scope.Dialect.BuildCreateTableSql(request));
    }

    public async Task CreateTableAsync(TenantId tenantId, long databaseId, CreateTableRequest request, CancellationToken cancellationToken)
    {
        var ddl = await BuildCreateTableDdlAsync(tenantId, databaseId, new PreviewCreateTableDdlRequest(
            request.Schema,
            request.TableName,
            request.Comment,
            request.Columns,
            request.Options), cancellationToken);
        await ExecuteDraftSqlAsync(tenantId, databaseId, ddl.Ddl, cancellationToken);
    }

    public async Task CreateTableBySqlAsync(TenantId tenantId, long databaseId, CreateTableSqlRequest request, CancellationToken cancellationToken)
    {
        _sqlSafetyValidator.ValidateCreateTable(request.Sql);
        await ExecuteDraftSqlAsync(tenantId, databaseId, request.Sql, cancellationToken);
    }

    public async Task<PreviewDataResponse> PreviewViewSqlAsync(
        TenantId tenantId,
        long databaseId,
        PreviewViewSqlRequest request,
        CancellationToken cancellationToken)
    {
        _sqlSafetyValidator.ValidateSelectOnly(request.Sql);
        using var scope = await OpenAsync(tenantId, databaseId, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        var sql = scope.Dialect.LimitSelectSql(request.Sql, request.Limit);
        var table = await scope.Client.Ado.GetDataTableAsync(sql);
        return MapPreview(table, table.Rows.Count, 1, Math.Clamp(request.Limit, 1, 100));
    }

    public async Task CreateViewAsync(TenantId tenantId, long databaseId, CreateViewRequest request, CancellationToken cancellationToken)
    {
        _sqlSafetyValidator.ValidateCreateView(request.Sql);
        using var scope = await OpenAsync(tenantId, databaseId, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        var sql = request.Sql.TrimStart().StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
            ? request.Sql
            : scope.Dialect.BuildCreateViewSql(request);
        await scope.Client.Ado.ExecuteCommandAsync(sql);
    }

    public async Task DropTableAsync(
        TenantId tenantId,
        long databaseId,
        string tableName,
        DropDatabaseObjectRequest request,
        CancellationToken cancellationToken)
        => await DropObjectAsync(tenantId, databaseId, tableName, request, "table", cancellationToken);

    public async Task DropViewAsync(
        TenantId tenantId,
        long databaseId,
        string viewName,
        DropDatabaseObjectRequest request,
        CancellationToken cancellationToken)
        => await DropObjectAsync(tenantId, databaseId, viewName, request, "view", cancellationToken);

    private async Task<IReadOnlyList<DatabaseColumnDto>> GetColumnsAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string objectName,
        string? schema,
        CancellationToken cancellationToken)
    {
        using var scope = await OpenAsync(tenantId, databaseId, environment, cancellationToken);
        var table = await scope.Client.Ado.GetDataTableAsync(scope.Dialect.BuildColumnsSql(objectName, schema));
        return table.Rows.Cast<DataRow>().Select(MapColumn).ToList();
    }

    private async Task<DdlResponse> GetDdlAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string objectName,
        string? schema,
        string objectType,
        CancellationToken cancellationToken)
    {
        using var scope = await OpenAsync(tenantId, databaseId, environment, cancellationToken);
        var table = await scope.Client.Ado.GetDataTableAsync(scope.Dialect.BuildDdlSql(objectName, schema, objectType));
        if (table.Rows.Count == 0)
        {
            return new DdlResponse(string.Empty);
        }

        return new DdlResponse(ExtractDdl(table.Rows[0]));
    }

    private async Task<PreviewDataResponse> PreviewObjectDataAsync(
        TenantId tenantId,
        long databaseId,
        string objectName,
        PreviewDataRequest request,
        CancellationToken cancellationToken)
    {
        using var scope = await OpenAsync(tenantId, databaseId, request.Environment, cancellationToken);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var data = await scope.Client.Ado.GetDataTableAsync(scope.Dialect.BuildPagedSelectSql(objectName, request.Schema, pageIndex, pageSize));
        var totalObj = await scope.Client.Ado.GetScalarAsync(scope.Dialect.BuildCountSql(objectName, request.Schema));
        var total = Convert.ToInt64(totalObj);
        return MapPreview(data, total, pageIndex, pageSize);
    }

    private async Task ExecuteDraftSqlAsync(TenantId tenantId, long databaseId, string sql, CancellationToken cancellationToken)
    {
        using var scope = await OpenAsync(tenantId, databaseId, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        await scope.Client.Ado.ExecuteCommandAsync(sql);
    }

    private async Task DropObjectAsync(
        TenantId tenantId,
        long databaseId,
        string objectName,
        DropDatabaseObjectRequest request,
        string objectType,
        CancellationToken cancellationToken)
    {
        if (!request.ConfirmDanger || !string.Equals(request.ConfirmName, objectName, StringComparison.Ordinal))
        {
            throw new BusinessException("危险操作确认信息不匹配。", ErrorCodes.ValidationError);
        }

        using var scope = await OpenAsync(tenantId, databaseId, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        await scope.Client.Ado.ExecuteCommandAsync(scope.Dialect.BuildDropSql(objectName, request.Schema, objectType));
    }

    private async Task<DatabaseScope> OpenAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var (database, client) = await _clientFactory.CreateClientAsync(tenantId, databaseId, environment, cancellationToken);
        client.Ado.CommandTimeOut = QueryTimeoutSeconds;
        return new DatabaseScope(database, client, _dialects.Resolve(database.DriverCode));
    }

    private static DatabaseObjectDto MapObject(DataRow row)
        => new(
            ReadString(row, "name") ?? string.Empty,
            ReadString(row, "object_type") ?? "table",
            ReadString(row, "schema_name"),
            ReadString(row, "engine"),
            ReadLong(row, "row_count"),
            ReadString(row, "comment"),
            ReadDateTime(row, "created_at"),
            ReadDateTime(row, "updated_at"));

    private static DatabaseColumnDto MapColumn(DataRow row)
    {
        var isSqlitePragma = row.Table.Columns.Contains("cid") && row.Table.Columns.Contains("type");
        if (isSqlitePragma)
        {
            var pk = ReadLong(row, "pk") is > 0;
            return new DatabaseColumnDto(
                ReadString(row, "name") ?? string.Empty,
                ReadString(row, "type") ?? "TEXT",
                ReadString(row, "type"),
                null,
                null,
                null,
                ReadLong(row, "notnull") != 1,
                pk,
                pk && string.Equals(ReadString(row, "type"), "INTEGER", StringComparison.OrdinalIgnoreCase),
                ReadString(row, "dflt_value"),
                null,
                Convert.ToInt32(ReadLong(row, "cid") ?? 0) + 1);
        }

        var columnKey = ReadString(row, "column_key");
        var extra = ReadString(row, "extra");
        return new DatabaseColumnDto(
            ReadString(row, "name") ?? string.Empty,
            ReadString(row, "data_type") ?? "unknown",
            ReadString(row, "raw_data_type"),
            ReadInt(row, "length"),
            ReadInt(row, "precision"),
            ReadInt(row, "scale"),
            ReadBoolish(row, "nullable"),
            string.Equals(columnKey, "PRI", StringComparison.OrdinalIgnoreCase) || ReadBoolish(row, "primary_key"),
            (extra?.Contains("auto_increment", StringComparison.OrdinalIgnoreCase) ?? false) || ReadBoolish(row, "auto_increment"),
            ReadString(row, "default_value"),
            ReadString(row, "comment"),
            ReadInt(row, "ordinal") ?? 0);
    }

    private static PreviewDataResponse MapPreview(DataTable table, long total, int pageIndex, int pageSize)
    {
        var columns = table.Columns.Cast<DataColumn>().Select(x => new PreviewDataColumn(x.ColumnName, x.DataType.Name)).ToList();
        var rows = new List<IReadOnlyDictionary<string, object?>>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn column in table.Columns)
            {
                item[column.ColumnName] = row[column] == DBNull.Value ? null : row[column];
            }

            rows.Add(item);
        }

        return new PreviewDataResponse(columns, rows, total, pageIndex, pageSize);
    }

    private static string ExtractDdl(DataRow row)
    {
        foreach (DataColumn column in row.Table.Columns)
        {
            var name = column.ColumnName;
            if (string.Equals(name, "ddl", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Create Table", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Create View", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "sql", StringComparison.OrdinalIgnoreCase))
            {
                return row[column]?.ToString() ?? string.Empty;
            }
        }

        return row.ItemArray.LastOrDefault()?.ToString() ?? string.Empty;
    }

    private static string? ReadString(DataRow row, string column)
        => row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? row[column]?.ToString() : null;

    private static long? ReadLong(DataRow row, string column)
        => row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToInt64(row[column]) : null;

    private static int? ReadInt(DataRow row, string column)
        => row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToInt32(row[column]) : null;

    private static DateTime? ReadDateTime(DataRow row, string column)
        => row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToDateTime(row[column]) : null;

    private static bool ReadBoolish(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
        {
            return false;
        }

        var value = row[column];
        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s => string.Equals(s, "YES", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s, "1", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private sealed class DatabaseScope : IDisposable
    {
        public DatabaseScope(AiDatabase database, SqlSugar.SqlSugarClient client, IDatabaseDialect dialect)
        {
            Database = database;
            Client = client;
            Dialect = dialect;
        }

        public AiDatabase Database { get; }

        public SqlSugar.SqlSugarClient Client { get; }

        public IDatabaseDialect Dialect { get; }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
