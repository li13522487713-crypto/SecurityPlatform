using System.Data;
using System.Text;
using Atlas.Application.AiPlatform.Models;
using SqlSugar;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public interface IDatabaseDialect
{
    string DriverCode { get; }

    string QuoteIdentifier(string name);

    string QuoteFullName(string? schema, string name);

    void ValidateIdentifier(string name, string parameterName);

    string NormalizeDataType(string logicalType, int? length, int? precision, int? scale);

    string BuildCreateDatabaseSql(string databaseName);

    string BuildDropDatabaseSql(string databaseName);

    string BuildCreateTableSql(CreateTableDefinition definition);

    string BuildCreateViewSql(CreateViewDefinition definition);

    string BuildPagedSelectSql(string objectName, string? schema, int pageIndex, int pageSize);

    string BuildSelectPreviewSql(string selectSql, int limit);

    string GetTableListSql(string? schema);

    string GetViewListSql(string? schema);

    string GetProcedureListSql(string? schema);

    string GetFunctionListSql(string? schema);

    string GetTriggerListSql(string? schema);

    string GetEventListSql(string? schema);

    string GetColumnListSql(string objectName, string? schema);

    Task<string> GetTableDdlAsync(ISqlSugarClient db, string tableName, string? schema, CancellationToken cancellationToken);

    Task<string> GetViewDdlAsync(ISqlSugarClient db, string viewName, string? schema, CancellationToken cancellationToken);

    bool SupportsCreateDatabase { get; }

    bool SupportsCreateSchema { get; }

    bool SupportsEstimatedRowCount { get; }

    string BuildListObjectsSql(string objectType);

    string BuildColumnsSql(string objectName, string? schema);

    string BuildDdlSql(string objectName, string? schema, string objectType);

    string BuildCountSql(string objectName, string? schema);

    string BuildCreateTableSql(PreviewCreateTableDdlRequest request);

    string BuildCreateViewSql(CreateViewRequest request);

    string BuildDropSql(string objectName, string? schema, string objectType);

    string LimitSelectSql(string selectSql, int limit);

    void ValidateIdentifier(string identifier);
}

public interface IDatabaseDialectRegistry
{
    IDatabaseDialect Resolve(string driverCode);

    IReadOnlyList<string> ListSupported();
}

public sealed class DatabaseDialectRegistry : IDatabaseDialectRegistry
{
    private readonly IReadOnlyDictionary<string, IDatabaseDialect> _dialects;

    public DatabaseDialectRegistry(IEnumerable<IDatabaseDialect> dialects)
    {
        _dialects = dialects.ToDictionary(x => x.DriverCode, StringComparer.OrdinalIgnoreCase);
    }

    public IDatabaseDialect Resolve(string driverCode)
    {
        var normalized = Atlas.Infrastructure.Services.DataSourceDriverRegistry.NormalizeDriverCode(driverCode);
        if (_dialects.TryGetValue(normalized, out var dialect))
        {
            return dialect;
        }

        throw new NotSupportedException($"Database provider {driverCode} is not supported for structure management.");
    }

    public IReadOnlyList<string> ListSupported()
        => _dialects.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
}

public abstract class DatabaseDialectBase : IDatabaseDialect
{
    public abstract string DriverCode { get; }

    protected virtual IReadOnlySet<string> SupportedDataTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BIGINT",
        "INT",
        "INTEGER",
        "VARCHAR",
        "NVARCHAR",
        "TEXT",
        "REAL",
        "NUMERIC",
        "DECIMAL",
        "BOOLEAN",
        "BIT",
        "DATETIME",
        "DATETIME2",
        "TIMESTAMP",
        "DATE",
        "BLOB",
        "JSON",
        "JSONB",
        "UUID",
        "UNIQUEIDENTIFIER",
        "NUMBER",
        "VARCHAR2",
        "NVARCHAR2",
        "CLOB"
    };

    public virtual bool SupportsCreateDatabase => false;

    public virtual bool SupportsCreateSchema => false;

    public virtual bool SupportsEstimatedRowCount => false;

    public virtual string QuoteIdentifier(string identifier)
    {
        ValidateIdentifier(identifier);
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    public virtual string QuoteFullName(string? schema, string name)
        => QualifiedName(name, schema);

    public virtual void ValidateIdentifier(string name, string parameterName)
    {
        try
        {
            ValidateIdentifier(name);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"{parameterName}: {ex.Message}", ex);
        }
    }

    public virtual string NormalizeDataType(string logicalType, int? length, int? precision, int? scale)
    {
        var dataType = string.IsNullOrWhiteSpace(logicalType) ? "TEXT" : logicalType.Trim().ToUpperInvariant();
        if (!SupportedDataTypes.Contains(dataType))
        {
            throw new InvalidOperationException($"Data type {logicalType} is not supported by {DriverCode}.");
        }

        if (length.HasValue && length.Value > 0 && IsLengthType(dataType))
        {
            return $"{dataType}({length.Value})";
        }

        if (precision.HasValue && precision.Value > 0 && IsPrecisionType(dataType))
        {
            return scale.HasValue && scale.Value >= 0
                ? $"{dataType}({precision.Value},{scale.Value})"
                : $"{dataType}({precision.Value})";
        }

        return dataType;
    }

    public virtual string BuildCreateDatabaseSql(string databaseName)
    {
        ValidateIdentifier(databaseName, nameof(databaseName));
        return $"CREATE DATABASE {QuoteIdentifier(databaseName)}";
    }

    public virtual string BuildDropDatabaseSql(string databaseName)
    {
        ValidateIdentifier(databaseName, nameof(databaseName));
        return $"DROP DATABASE {QuoteIdentifier(databaseName)}";
    }

    public virtual string BuildCreateTableSql(CreateTableDefinition definition)
    {
        var request = new PreviewCreateTableDdlRequest(
            definition.Schema,
            definition.TableName,
            definition.Comment,
            definition.Columns
                .OrderBy(column => column.Ordinal)
                .Select(column => new TableColumnDesignDto(
                    column.Name,
                    column.DataType,
                    column.Length,
                    column.Precision,
                    column.Scale,
                    column.Nullable,
                    column.PrimaryKey,
                    column.AutoIncrement,
                    column.DefaultValue,
                    column.Comment))
                .ToList(),
            definition.Options is null
                ? null
                : new TableOptionsDto(
                    definition.Options.Engine,
                    definition.Options.Charset,
                    definition.Options.Collation,
                    definition.Options.Schema,
                    definition.Options.Tablespace));
        return BuildCreateTableSql(request);
    }

    public virtual string BuildCreateViewSql(CreateViewDefinition definition)
    {
        ValidateIdentifier(definition.ViewName, nameof(definition.ViewName));
        var sql = definition.Mode == CreateViewMode.CreateViewSql
            ? definition.CreateSql?.Trim()
            : $"CREATE VIEW {QualifiedName(definition.ViewName, definition.Schema)} AS{Environment.NewLine}{definition.SelectSql?.Trim().TrimEnd(';')}";
        return string.IsNullOrWhiteSpace(sql) ? throw new InvalidOperationException("View SQL is required.") : sql.Trim().TrimEnd(';') + ";";
    }

    public abstract string BuildListObjectsSql(string objectType);

    public abstract string BuildColumnsSql(string objectName, string? schema);

    public abstract string BuildDdlSql(string objectName, string? schema, string objectType);

    public virtual string GetTableListSql(string? schema) => BuildListObjectsSql("table");

    public virtual string GetViewListSql(string? schema) => BuildListObjectsSql("view");

    public virtual string GetProcedureListSql(string? schema) => BuildListObjectsSql("procedure");

    public virtual string GetFunctionListSql(string? schema) => BuildListObjectsSql("function");

    public virtual string GetTriggerListSql(string? schema) => BuildListObjectsSql("trigger");

    public virtual string GetEventListSql(string? schema) => BuildListObjectsSql("event");

    public virtual string GetColumnListSql(string objectName, string? schema) => BuildColumnsSql(objectName, schema);

    public virtual async Task<string> GetTableDdlAsync(ISqlSugarClient db, string tableName, string? schema, CancellationToken cancellationToken)
        => await GetDdlAsync(db, tableName, schema, "table", cancellationToken);

    public virtual async Task<string> GetViewDdlAsync(ISqlSugarClient db, string viewName, string? schema, CancellationToken cancellationToken)
        => await GetDdlAsync(db, viewName, schema, "view", cancellationToken);

    public virtual string BuildPagedSelectSql(string objectName, string? schema, int pageIndex, int pageSize)
    {
        var offset = Math.Max(0, pageIndex - 1) * Math.Clamp(pageSize, 1, 100);
        return $"SELECT * FROM {QualifiedName(objectName, schema)} LIMIT {Math.Clamp(pageSize, 1, 100)} OFFSET {offset}";
    }

    public virtual string BuildCountSql(string objectName, string? schema)
        => $"SELECT COUNT(*) FROM {QualifiedName(objectName, schema)}";

    public virtual string BuildCreateTableSql(PreviewCreateTableDdlRequest request)
    {
        ValidateIdentifier(request.TableName);
        if (request.Columns.Count == 0)
        {
            throw new InvalidOperationException("At least one column is required.");
        }

        var lines = request.Columns.Select(BuildColumnSql).ToList();
        var primaryKeys = request.Columns.Where(x => x.PrimaryKey).Select(x => QuoteIdentifier(x.Name)).ToList();
        if (primaryKeys.Count > 0)
        {
            lines.Add($"PRIMARY KEY ({string.Join(", ", primaryKeys)})");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"CREATE TABLE {QualifiedName(request.TableName, request.Schema)} (");
        builder.AppendLine("  " + string.Join("," + Environment.NewLine + "  ", lines));
        builder.Append(')');
        AppendTableOptions(builder, request);
        builder.Append(';');
        return builder.ToString();
    }

    public virtual string BuildCreateViewSql(CreateViewRequest request)
    {
        ValidateIdentifier(request.ViewName);
        return $"CREATE VIEW {QualifiedName(request.ViewName, request.Schema)} AS{Environment.NewLine}{request.Sql.Trim().TrimEnd(';')};";
    }

    public virtual string BuildDropSql(string objectName, string? schema, string objectType)
    {
        ValidateIdentifier(objectName);
        var keyword = string.Equals(objectType, "view", StringComparison.OrdinalIgnoreCase) ? "VIEW" : "TABLE";
        return $"DROP {keyword} {QualifiedName(objectName, schema)}";
    }

    public virtual string LimitSelectSql(string selectSql, int limit)
        => $"SELECT * FROM ({selectSql.Trim().TrimEnd(';')}) atlas_preview LIMIT {Math.Clamp(limit, 1, 100)}";

    public virtual string BuildSelectPreviewSql(string selectSql, int limit)
        => LimitSelectSql(selectSql, limit);

    public virtual void ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("Identifier is required.");
        }

        if (identifier.Contains('.', StringComparison.Ordinal) ||
            !identifier.All(ch => (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || char.IsDigit(ch) || ch == '_'))
        {
            throw new InvalidOperationException($"Identifier {identifier} contains unsupported characters.");
        }
    }

    protected virtual string QualifiedName(string objectName, string? schema)
    {
        ValidateIdentifier(objectName);
        if (string.IsNullOrWhiteSpace(schema))
        {
            return QuoteIdentifier(objectName);
        }

        ValidateIdentifier(schema);
        return $"{QuoteIdentifier(schema)}.{QuoteIdentifier(objectName)}";
    }

    protected virtual string BuildColumnSql(TableColumnDesignDto column)
    {
        ValidateIdentifier(column.Name);
        var type = NormalizeType(column);
        var pieces = new List<string> { QuoteIdentifier(column.Name), type };
        if (!column.Nullable || column.PrimaryKey)
        {
            pieces.Add("NOT NULL");
        }

        if (!string.IsNullOrWhiteSpace(column.DefaultValue))
        {
            pieces.Add($"DEFAULT {ValidateDefaultValue(column.DefaultValue!.Trim())}");
        }

        return string.Join(' ', pieces);
    }

    protected virtual string NormalizeType(TableColumnDesignDto column)
    {
        return NormalizeDataType(column.DataType, column.Length, column.Precision, column.Scale);
    }

    protected virtual bool IsLengthType(string dataType)
        => dataType.Contains("CHAR", StringComparison.OrdinalIgnoreCase) || dataType is "VARCHAR" or "NVARCHAR";

    protected virtual bool IsPrecisionType(string dataType)
        => dataType is "DECIMAL" or "NUMERIC" or "NUMBER";

    protected virtual void AppendTableOptions(StringBuilder builder, PreviewCreateTableDdlRequest request)
    {
    }

    protected virtual string ValidateDefaultValue(string value)
    {
        if (value.Contains(';', StringComparison.Ordinal) ||
            value.Contains("--", StringComparison.Ordinal) ||
            value.Contains("/*", StringComparison.Ordinal) ||
            value.Contains("*/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Default value contains unsafe SQL tokens.");
        }

        return value;
    }

    private async Task<string> GetDdlAsync(ISqlSugarClient db, string objectName, string? schema, string objectType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var table = await db.Ado.GetDataTableAsync(BuildDdlSql(objectName, schema, objectType));
        if (table.Rows.Count == 0)
        {
            return string.Empty;
        }

        foreach (System.Data.DataColumn column in table.Columns)
        {
            var name = column.ColumnName;
            if (string.Equals(name, "ddl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "sql", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Create Table", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Create View", StringComparison.OrdinalIgnoreCase))
            {
                return table.Rows[0][column]?.ToString() ?? string.Empty;
            }
        }

        return table.Rows[0].ItemArray.LastOrDefault()?.ToString() ?? string.Empty;
    }
}
