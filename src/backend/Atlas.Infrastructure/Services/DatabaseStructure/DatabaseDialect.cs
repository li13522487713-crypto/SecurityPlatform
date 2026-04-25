using System.Data;
using System.Text;
using Atlas.Application.AiPlatform.Models;
using SqlSugar;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public interface IDatabaseDialect
{
    string DriverCode { get; }

    string QuoteIdentifier(string identifier);

    string BuildListObjectsSql(string objectType);

    string BuildColumnsSql(string objectName, string? schema);

    string BuildDdlSql(string objectName, string? schema, string objectType);

    string BuildPagedSelectSql(string objectName, string? schema, int pageIndex, int pageSize);

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
}

public abstract class DatabaseDialectBase : IDatabaseDialect
{
    public abstract string DriverCode { get; }

    public virtual string QuoteIdentifier(string identifier)
    {
        ValidateIdentifier(identifier);
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    public abstract string BuildListObjectsSql(string objectType);

    public abstract string BuildColumnsSql(string objectName, string? schema);

    public abstract string BuildDdlSql(string objectName, string? schema, string objectType);

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

    public virtual void ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("Identifier is required.");
        }

        if (!identifier.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
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
            pieces.Add($"DEFAULT {column.DefaultValue!.Trim()}");
        }

        return string.Join(' ', pieces);
    }

    protected virtual string NormalizeType(TableColumnDesignDto column)
    {
        var dataType = string.IsNullOrWhiteSpace(column.DataType) ? "TEXT" : column.DataType.Trim().ToUpperInvariant();
        if (column.Length.HasValue && column.Length.Value > 0 && IsLengthType(dataType))
        {
            return $"{dataType}({column.Length.Value})";
        }

        if (column.Precision.HasValue && column.Precision.Value > 0 && IsPrecisionType(dataType))
        {
            return column.Scale.HasValue && column.Scale.Value >= 0
                ? $"{dataType}({column.Precision.Value},{column.Scale.Value})"
                : $"{dataType}({column.Precision.Value})";
        }

        return dataType;
    }

    protected virtual bool IsLengthType(string dataType)
        => dataType.Contains("CHAR", StringComparison.OrdinalIgnoreCase) || dataType is "VARCHAR" or "NVARCHAR";

    protected virtual bool IsPrecisionType(string dataType)
        => dataType is "DECIMAL" or "NUMERIC" or "NUMBER";

    protected virtual void AppendTableOptions(StringBuilder builder, PreviewCreateTableDdlRequest request)
    {
    }
}
