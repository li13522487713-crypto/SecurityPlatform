using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.DatabaseStructure;

public class PostgreSqlDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "PostgreSQL";

    public override bool SupportsCreateDatabase => true;

    public override bool SupportsCreateSchema => true;

    protected override IReadOnlySet<string> SupportedDataTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BIGINT",
        "INTEGER",
        "VARCHAR",
        "TEXT",
        "TIMESTAMP",
        "NUMERIC",
        "DECIMAL",
        "BOOLEAN",
        "JSONB",
        "UUID"
    };

    public override string BuildListObjectsSql(string objectType)
    {
        var type = objectType.Trim().ToLowerInvariant();
        return type switch
        {
            "view" => "SELECT table_name AS name, 'view' AS object_type, table_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.views WHERE table_schema NOT IN ('pg_catalog','information_schema') ORDER BY table_schema, table_name",
            "procedure" => "SELECT routine_name AS name, 'procedure' AS object_type, routine_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.routines WHERE routine_type = 'PROCEDURE' AND routine_schema NOT IN ('pg_catalog','information_schema') ORDER BY routine_schema, routine_name",
            "trigger" => "SELECT trigger_name AS name, 'trigger' AS object_type, trigger_schema AS schema_name, NULL AS engine, NULL AS row_count, event_manipulation AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.triggers WHERE trigger_schema NOT IN ('pg_catalog','information_schema') ORDER BY trigger_schema, trigger_name",
            _ => "SELECT table_name AS name, 'table' AS object_type, table_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_type = 'BASE TABLE' ORDER BY table_schema, table_name"
        };
    }

    public override string GetTableListSql(string? schema)
        => BuildListObjectsSql("table", schema);

    public override string GetViewListSql(string? schema)
        => BuildListObjectsSql("view", schema);

    public override string GetProcedureListSql(string? schema)
        => BuildListObjectsSql("procedure", schema);

    public override string GetTriggerListSql(string? schema)
        => BuildListObjectsSql("trigger", schema);

    private string BuildListObjectsSql(string objectType, string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return BuildListObjectsSql(objectType);
        }

        ValidateIdentifier(schema);
        var schemaLiteral = schema.Replace("'", "''", StringComparison.Ordinal);
        var type = objectType.Trim().ToLowerInvariant();
        return type switch
        {
            "view" => $"SELECT table_name AS name, 'view' AS object_type, table_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.views WHERE table_schema = '{schemaLiteral}' ORDER BY table_schema, table_name",
            "procedure" => $"SELECT routine_name AS name, 'procedure' AS object_type, routine_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.routines WHERE routine_type = 'PROCEDURE' AND routine_schema = '{schemaLiteral}' ORDER BY routine_schema, routine_name",
            "trigger" => $"SELECT trigger_name AS name, 'trigger' AS object_type, trigger_schema AS schema_name, NULL AS engine, NULL AS row_count, event_manipulation AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.triggers WHERE trigger_schema = '{schemaLiteral}' ORDER BY trigger_schema, trigger_name",
            _ => $"SELECT table_name AS name, 'table' AS object_type, table_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.tables WHERE table_schema = '{schemaLiteral}' AND table_type = 'BASE TABLE' ORDER BY table_schema, table_name"
        };
    }

    public override string BuildColumnsSql(string objectName, string? schema)
    {
        ValidateIdentifier(objectName);
        var schemaPredicate = string.IsNullOrWhiteSpace(schema)
            ? "table_schema = current_schema()"
            : $"table_schema = '{schema.Replace("'", "''", StringComparison.Ordinal)}'";
        return "SELECT column_name AS name, data_type AS data_type, udt_name AS raw_data_type, character_maximum_length AS length, numeric_precision AS precision, numeric_scale AS scale, is_nullable AS nullable, column_default AS default_value, ordinal_position AS ordinal " +
            $"FROM information_schema.columns WHERE {schemaPredicate} AND table_name = '{objectName.Replace("'", "''", StringComparison.Ordinal)}' ORDER BY ordinal_position";
    }

    public override string BuildForeignKeysSql(string tableName, string? schema)
    {
        ValidateIdentifier(tableName);
        var schemaPredicate = string.IsNullOrWhiteSpace(schema)
            ? "tc.table_schema = current_schema()"
            : $"tc.table_schema = '{schema.Replace("'", "''", StringComparison.Ordinal)}'";
        return
            "SELECT " +
            "tc.constraint_name AS foreign_key_name, " +
            "tc.table_name AS table_name, " +
            "tc.table_schema AS schema_name, " +
            "kcu.column_name AS source_column_name, " +
            "ccu.table_name AS referenced_table_name, " +
            "ccu.table_schema AS referenced_schema_name, " +
            "ccu.column_name AS referenced_column_name, " +
            "rc.delete_rule AS on_delete, " +
            "rc.update_rule AS on_update, " +
            "kcu.ordinal_position AS ordinal_position " +
            "FROM information_schema.table_constraints tc " +
            "INNER JOIN information_schema.key_column_usage kcu " +
            "ON kcu.constraint_name = tc.constraint_name " +
            "AND kcu.constraint_schema = tc.constraint_schema " +
            "INNER JOIN information_schema.constraint_column_usage ccu " +
            "ON ccu.constraint_name = tc.constraint_name " +
            "AND ccu.constraint_schema = tc.constraint_schema " +
            "INNER JOIN information_schema.referential_constraints rc " +
            "ON rc.constraint_name = tc.constraint_name " +
            "AND rc.constraint_schema = tc.constraint_schema " +
            $"WHERE tc.constraint_type = 'FOREIGN KEY' AND {schemaPredicate} AND tc.table_name = '{tableName.Replace("'", "''", StringComparison.Ordinal)}' " +
            "ORDER BY tc.constraint_name, kcu.ordinal_position";
    }

    public override string BuildDdlSql(string objectName, string? schema, string objectType)
    {
        ValidateIdentifier(objectName);
        var objectKind = string.Equals(objectType, "view", StringComparison.OrdinalIgnoreCase) ? "v" : "r";
        var qualified = string.IsNullOrWhiteSpace(schema) ? objectName : $"{schema}.{objectName}";
        return objectKind == "v"
            ? $"SELECT pg_get_viewdef('{qualified.Replace("'", "''", StringComparison.Ordinal)}'::regclass, true) AS ddl"
            : $"SELECT 'CREATE TABLE ' || '{qualified.Replace("'", "''", StringComparison.Ordinal)}' || E' (\\n' || string_agg('  ' || quote_ident(column_name) || ' ' || udt_name || CASE WHEN is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END, E',\\n' ORDER BY ordinal_position) || E'\\n);' AS ddl FROM information_schema.columns WHERE table_name = '{objectName.Replace("'", "''", StringComparison.Ordinal)}' {(string.IsNullOrWhiteSpace(schema) ? "AND table_schema = current_schema()" : $"AND table_schema = '{schema.Replace("'", "''", StringComparison.Ordinal)}'")}";
    }

    public override string BuildDropSql(string objectName, string? schema, string objectType)
    {
        ValidateIdentifier(objectName);
        var keyword = string.Equals(objectType, "view", StringComparison.OrdinalIgnoreCase) ? "VIEW" : "TABLE";
        return $"DROP {keyword} IF EXISTS {QualifiedName(objectName, schema)}";
    }

    public override string BuildAlterColumnSql(string tableName, string? schema, string columnName, TableColumnDesignDto column)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(columnName);
        var statements = new List<string>
        {
            $"ALTER TABLE {QualifiedName(tableName, schema)} ALTER COLUMN {QuoteIdentifier(columnName)} TYPE {NormalizeType(column)}",
            $"ALTER TABLE {QualifiedName(tableName, schema)} ALTER COLUMN {QuoteIdentifier(columnName)} {(column.Nullable && !column.PrimaryKey ? "DROP" : "SET")} NOT NULL"
        };

        if (string.IsNullOrWhiteSpace(column.DefaultValue))
        {
            statements.Add($"ALTER TABLE {QualifiedName(tableName, schema)} ALTER COLUMN {QuoteIdentifier(columnName)} DROP DEFAULT");
        }
        else
        {
            statements.Add($"ALTER TABLE {QualifiedName(tableName, schema)} ALTER COLUMN {QuoteIdentifier(columnName)} SET DEFAULT {column.DefaultValue!.Trim()}");
        }

        return string.Join(";" + Environment.NewLine, statements) + ";";
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
        return $"ALTER TABLE {QualifiedName(request.TableName, request.Schema)} DROP CONSTRAINT {QuoteIdentifier(request.ForeignKeyName)};";
    }

    protected override string BuildColumnSql(TableColumnDesignDto column)
    {
        if (column.AutoIncrement)
        {
            ValidateIdentifier(column.Name);
            if (!column.PrimaryKey)
            {
                throw new InvalidOperationException("Identity column must be a primary key.");
            }

            var type = column.DataType.Equals("BIGINT", StringComparison.OrdinalIgnoreCase) ? "BIGINT" : "INTEGER";
            return $"{QuoteIdentifier(column.Name)} {type} GENERATED BY DEFAULT AS IDENTITY NOT NULL";
        }

        return base.BuildColumnSql(column);
    }

    protected override string NormalizeType(TableColumnDesignDto column)
    {
        if (string.Equals(column.DataType, "DATETIME", StringComparison.OrdinalIgnoreCase))
        {
            return "TIMESTAMP";
        }

        return base.NormalizeType(column);
    }

    private static string NormalizeReferentialAction(string? action)
    {
        var normalized = string.IsNullOrWhiteSpace(action) ? "NO ACTION" : action.Trim().ToUpperInvariant();
        return normalized switch
        {
            "CASCADE" => "CASCADE",
            "SET NULL" => "SET NULL",
            "SET DEFAULT" => "SET DEFAULT",
            "RESTRICT" => "RESTRICT",
            "NO ACTION" => "NO ACTION",
            _ => throw new InvalidOperationException($"PostgreSQL foreign key action {action} is not supported.")
        };
    }
}
