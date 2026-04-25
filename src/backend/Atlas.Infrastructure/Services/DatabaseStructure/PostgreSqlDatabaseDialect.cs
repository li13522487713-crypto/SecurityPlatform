namespace Atlas.Infrastructure.Services.DatabaseStructure;

public class PostgreSqlDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "PostgreSQL";

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

    public override string BuildColumnsSql(string objectName, string? schema)
    {
        ValidateIdentifier(objectName);
        var schemaPredicate = string.IsNullOrWhiteSpace(schema)
            ? "table_schema = current_schema()"
            : $"table_schema = '{schema.Replace("'", "''", StringComparison.Ordinal)}'";
        return "SELECT column_name AS name, data_type AS data_type, udt_name AS raw_data_type, character_maximum_length AS length, numeric_precision AS precision, numeric_scale AS scale, is_nullable AS nullable, column_default AS default_value, ordinal_position AS ordinal " +
            $"FROM information_schema.columns WHERE {schemaPredicate} AND table_name = '{objectName.Replace("'", "''", StringComparison.Ordinal)}' ORDER BY ordinal_position";
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
}
