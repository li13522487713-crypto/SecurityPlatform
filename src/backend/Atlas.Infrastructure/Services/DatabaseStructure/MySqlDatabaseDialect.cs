using System.Text;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed class MySqlDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "MySql";

    public override string QuoteIdentifier(string identifier)
    {
        ValidateIdentifier(identifier);
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    public override string BuildListObjectsSql(string objectType)
    {
        var type = objectType.Trim().ToLowerInvariant();
        return type switch
        {
            "view" => "SELECT TABLE_NAME AS name, 'view' AS object_type, TABLE_SCHEMA AS schema_name, NULL AS engine, NULL AS row_count, TABLE_COMMENT AS comment, CREATE_TIME AS created_at, UPDATE_TIME AS updated_at FROM information_schema.VIEWS JOIN information_schema.TABLES USING (TABLE_SCHEMA, TABLE_NAME) WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME",
            "procedure" => "SELECT ROUTINE_NAME AS name, 'procedure' AS object_type, ROUTINE_SCHEMA AS schema_name, NULL AS engine, NULL AS row_count, ROUTINE_COMMENT AS comment, CREATED AS created_at, LAST_ALTERED AS updated_at FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'PROCEDURE' ORDER BY ROUTINE_NAME",
            "trigger" => "SELECT TRIGGER_NAME AS name, 'trigger' AS object_type, TRIGGER_SCHEMA AS schema_name, NULL AS engine, NULL AS row_count, ACTION_STATEMENT AS comment, CREATED AS created_at, NULL AS updated_at FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE() ORDER BY TRIGGER_NAME",
            _ => "SELECT TABLE_NAME AS name, 'table' AS object_type, TABLE_SCHEMA AS schema_name, ENGINE AS engine, TABLE_ROWS AS row_count, TABLE_COMMENT AS comment, CREATE_TIME AS created_at, UPDATE_TIME AS updated_at FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME"
        };
    }

    public override string BuildColumnsSql(string objectName, string? schema)
    {
        ValidateIdentifier(objectName);
        var schemaPredicate = string.IsNullOrWhiteSpace(schema)
            ? "TABLE_SCHEMA = DATABASE()"
            : $"TABLE_SCHEMA = '{schema.Replace("'", "''", StringComparison.Ordinal)}'";
        return "SELECT COLUMN_NAME AS name, DATA_TYPE AS data_type, COLUMN_TYPE AS raw_data_type, CHARACTER_MAXIMUM_LENGTH AS length, NUMERIC_PRECISION AS precision, NUMERIC_SCALE AS scale, IS_NULLABLE AS nullable, COLUMN_KEY AS column_key, EXTRA AS extra, COLUMN_DEFAULT AS default_value, COLUMN_COMMENT AS comment, ORDINAL_POSITION AS ordinal " +
            $"FROM information_schema.COLUMNS WHERE {schemaPredicate} AND TABLE_NAME = '{objectName.Replace("'", "''", StringComparison.Ordinal)}' ORDER BY ORDINAL_POSITION";
    }

    public override string BuildDdlSql(string objectName, string? schema, string objectType)
    {
        ValidateIdentifier(objectName);
        return string.Equals(objectType, "view", StringComparison.OrdinalIgnoreCase)
            ? $"SHOW CREATE VIEW {QualifiedName(objectName, schema)}"
            : $"SHOW CREATE TABLE {QualifiedName(objectName, schema)}";
    }

    public override string LimitSelectSql(string selectSql, int limit)
        => $"SELECT * FROM ({selectSql.Trim().TrimEnd(';')}) atlas_preview LIMIT {Math.Clamp(limit, 1, 100)}";

    protected override string BuildColumnSql(TableColumnDesignDto column)
    {
        if (column.AutoIncrement)
        {
            ValidateIdentifier(column.Name);
            var pieces = new List<string> { QuoteIdentifier(column.Name), NormalizeType(column), "NOT NULL", "AUTO_INCREMENT" };
            return string.Join(' ', pieces);
        }

        return base.BuildColumnSql(column);
    }

    protected override void AppendTableOptions(StringBuilder builder, PreviewCreateTableDdlRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Options?.Engine))
        {
            builder.Append($" ENGINE={request.Options.Engine.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Options?.Charset))
        {
            builder.Append($" DEFAULT CHARSET={request.Options.Charset.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            builder.Append($" COMMENT='{request.Comment.Replace("'", "''", StringComparison.Ordinal)}'");
        }
    }
}
