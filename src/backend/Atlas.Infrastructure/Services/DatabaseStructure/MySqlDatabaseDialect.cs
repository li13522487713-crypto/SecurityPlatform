using System.Text;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed class MySqlDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "MySql";

    public override bool SupportsCreateDatabase => true;

    public override bool SupportsEstimatedRowCount => true;

    protected override IReadOnlySet<string> SupportedDataTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BIGINT",
        "INT",
        "VARCHAR",
        "TEXT",
        "DATETIME",
        "TIMESTAMP",
        "DECIMAL",
        "NUMERIC",
        "TINYINT",
        "JSON"
    };

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

    public override string BuildForeignKeysSql(string tableName, string? schema)
    {
        ValidateIdentifier(tableName);
        var schemaPredicate = string.IsNullOrWhiteSpace(schema)
            ? "kcu.TABLE_SCHEMA = DATABASE()"
            : $"kcu.TABLE_SCHEMA = '{schema.Replace("'", "''", StringComparison.Ordinal)}'";
        return
            "SELECT " +
            "kcu.CONSTRAINT_NAME AS foreign_key_name, " +
            "kcu.TABLE_NAME AS table_name, " +
            "kcu.TABLE_SCHEMA AS schema_name, " +
            "kcu.COLUMN_NAME AS source_column_name, " +
            "kcu.REFERENCED_TABLE_NAME AS referenced_table_name, " +
            "kcu.REFERENCED_TABLE_SCHEMA AS referenced_schema_name, " +
            "kcu.REFERENCED_COLUMN_NAME AS referenced_column_name, " +
            "rc.DELETE_RULE AS on_delete, " +
            "rc.UPDATE_RULE AS on_update, " +
            "kcu.ORDINAL_POSITION AS ordinal_position " +
            "FROM information_schema.KEY_COLUMN_USAGE kcu " +
            "INNER JOIN information_schema.REFERENTIAL_CONSTRAINTS rc " +
            "ON rc.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA " +
            "AND rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME " +
            $"WHERE {schemaPredicate} AND kcu.TABLE_NAME = '{tableName.Replace("'", "''", StringComparison.Ordinal)}' AND kcu.REFERENCED_TABLE_NAME IS NOT NULL " +
            "ORDER BY kcu.CONSTRAINT_NAME, kcu.ORDINAL_POSITION";
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

    public override string BuildAlterColumnSql(string tableName, string? schema, string columnName, TableColumnDesignDto column)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(columnName);
        return $"ALTER TABLE {QualifiedName(tableName, schema)} MODIFY COLUMN {BuildColumnSql(column)};";
    }

    public override string BuildRenameColumnSql(string tableName, string? schema, string columnName, string newColumnName)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(columnName);
        ValidateIdentifier(newColumnName);
        return $"ALTER TABLE {QualifiedName(tableName, schema)} RENAME COLUMN {QuoteIdentifier(columnName)} TO {QuoteIdentifier(newColumnName)};";
    }

    public override string BuildDropColumnSql(string tableName, string? schema, string columnName)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(columnName);
        return $"ALTER TABLE {QualifiedName(tableName, schema)} DROP COLUMN {QuoteIdentifier(columnName)};";
    }

    public override string BuildCreateForeignKeySql(CreateForeignKeyRequest request)
    {
        ValidateIdentifier(request.TableName);
        ValidateIdentifier(request.ForeignKeyName);
        var sourceColumns = request.SourceColumns.Select(QuoteIdentifier).ToArray();
        var referencedColumns = request.ReferencedColumns.Select(QuoteIdentifier).ToArray();
        return $"ALTER TABLE {QualifiedName(request.TableName, request.Schema)} ADD CONSTRAINT {QuoteIdentifier(request.ForeignKeyName)} FOREIGN KEY ({string.Join(", ", sourceColumns)}) REFERENCES {QualifiedName(request.ReferencedTableName, request.ReferencedSchema)} ({string.Join(", ", referencedColumns)}) ON DELETE {NormalizeReferentialAction(request.OnDelete)} ON UPDATE {NormalizeReferentialAction(request.OnUpdate)};";
    }

    public override string BuildDropForeignKeySql(DropForeignKeyRequest request)
    {
        ValidateIdentifier(request.TableName);
        ValidateIdentifier(request.ForeignKeyName);
        return $"ALTER TABLE {QualifiedName(request.TableName, request.Schema)} DROP FOREIGN KEY {QuoteIdentifier(request.ForeignKeyName)};";
    }

    protected override string BuildColumnSql(TableColumnDesignDto column)
    {
        if (column.AutoIncrement)
        {
            var normalizedType = NormalizeDataType(column.DataType, column.Length, column.Precision, column.Scale);
            if (!column.PrimaryKey || !IsMySqlIntegerType(normalizedType))
            {
                throw new InvalidOperationException("AUTO_INCREMENT is only allowed on integer primary key columns.");
            }

            ValidateIdentifier(column.Name);
            var pieces = new List<string> { QuoteIdentifier(column.Name), normalizedType, "NOT NULL", "AUTO_INCREMENT" };
            return string.Join(' ', pieces);
        }

        return base.BuildColumnSql(column);
    }

    protected override void AppendTableOptions(StringBuilder builder, PreviewCreateTableDdlRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Options?.Engine))
        {
            ValidateIdentifier(request.Options.Engine.Trim(), "engine");
            builder.Append($" ENGINE={request.Options.Engine.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Options?.Charset))
        {
            ValidateIdentifier(request.Options.Charset.Trim(), "charset");
            builder.Append($" DEFAULT CHARSET={request.Options.Charset.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Options?.Collation))
        {
            ValidateIdentifier(request.Options.Collation.Trim(), "collation");
            builder.Append($" COLLATE={request.Options.Collation.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            builder.Append($" COMMENT='{request.Comment.Replace("'", "''", StringComparison.Ordinal)}'");
        }
    }

    private static bool IsMySqlIntegerType(string dataType)
        => dataType.StartsWith("BIGINT", StringComparison.OrdinalIgnoreCase) ||
           dataType.StartsWith("INT", StringComparison.OrdinalIgnoreCase) ||
           dataType.StartsWith("TINYINT", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeReferentialAction(string? action)
    {
        var normalized = string.IsNullOrWhiteSpace(action) ? "NO ACTION" : action.Trim().ToUpperInvariant();
        return normalized switch
        {
            "CASCADE" => "CASCADE",
            "SET NULL" => "SET NULL",
            "RESTRICT" => "RESTRICT",
            "NO ACTION" => "NO ACTION",
            _ => throw new InvalidOperationException($"MySQL foreign key action {action} is not supported.")
        };
    }
}
