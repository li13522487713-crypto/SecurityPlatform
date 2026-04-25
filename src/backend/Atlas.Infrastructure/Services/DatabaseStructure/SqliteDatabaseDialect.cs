namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed class SqliteDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "SQLite";

    protected override IReadOnlySet<string> SupportedDataTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "INTEGER",
        "TEXT",
        "REAL",
        "NUMERIC",
        "BLOB"
    };

    public override string BuildListObjectsSql(string objectType)
    {
        var type = NormalizeObjectType(objectType);
        return type switch
        {
            "view" => "SELECT name, 'view' AS object_type, NULL AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM sqlite_master WHERE type = 'view' AND name NOT LIKE 'sqlite_%' ORDER BY name",
            "procedure" or "trigger" => type == "trigger"
                ? "SELECT name, 'trigger' AS object_type, NULL AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM sqlite_master WHERE type = 'trigger' ORDER BY name"
                : "SELECT '' AS name, 'procedure' AS object_type, NULL AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at WHERE 1 = 0",
            _ => "SELECT name, 'table' AS object_type, NULL AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name"
        };
    }

    public override string BuildColumnsSql(string objectName, string? schema)
    {
        ValidateIdentifier(objectName);
        return $"PRAGMA table_info({QuoteIdentifier(objectName)})";
    }

    public override string BuildDdlSql(string objectName, string? schema, string objectType)
    {
        ValidateIdentifier(objectName);
        var type = NormalizeObjectType(objectType);
        return $"SELECT sql AS ddl FROM sqlite_master WHERE type = '{type}' AND name = '{objectName.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    public override string BuildDropSql(string objectName, string? schema, string objectType)
    {
        ValidateIdentifier(objectName);
        var keyword = string.Equals(objectType, "view", StringComparison.OrdinalIgnoreCase) ? "VIEW" : "TABLE";
        return $"DROP {keyword} IF EXISTS {QuoteIdentifier(objectName)}";
    }

    protected override string BuildColumnSql(Atlas.Application.AiPlatform.Models.TableColumnDesignDto column)
    {
        if (column.PrimaryKey && column.AutoIncrement)
        {
            ValidateIdentifier(column.Name);
            return $"{QuoteIdentifier(column.Name)} INTEGER PRIMARY KEY AUTOINCREMENT";
        }

        return base.BuildColumnSql(column);
    }

    private static string NormalizeObjectType(string objectType)
    {
        var type = objectType.Trim().ToLowerInvariant();
        return type is "view" or "procedure" or "trigger" ? type : "table";
    }
}
