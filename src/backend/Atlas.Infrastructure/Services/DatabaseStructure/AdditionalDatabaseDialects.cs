namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed class SqlServerDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "SqlServer";

    public override bool SupportsCreateDatabase => true;

    public override string QuoteIdentifier(string identifier)
    {
        ValidateIdentifier(identifier);
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    public override string BuildListObjectsSql(string objectType)
    {
        var type = objectType.Trim().ToLowerInvariant();
        var predicate = type switch
        {
            "view" => "type = 'V'",
            "procedure" => "type = 'P'",
            "trigger" => "type = 'TR'",
            _ => "type = 'U'"
        };
        return $"SELECT name, CASE WHEN type = 'V' THEN 'view' WHEN type = 'P' THEN 'procedure' WHEN type = 'TR' THEN 'trigger' ELSE 'table' END AS object_type, SCHEMA_NAME(schema_id) AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, create_date AS created_at, modify_date AS updated_at FROM sys.objects WHERE {predicate} ORDER BY name";
    }

    public override string BuildColumnsSql(string objectName, string? schema)
        => $"SELECT c.name AS name, t.name AS data_type, t.name AS raw_data_type, c.max_length AS length, c.precision AS precision, c.scale AS scale, c.is_nullable AS nullable, CAST(CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS bit) AS primary_key, c.is_identity AS auto_increment, dc.definition AS default_value, c.column_id AS ordinal FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id JOIN sys.objects o ON c.object_id = o.object_id LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id LEFT JOIN (SELECT ic.object_id, ic.column_id FROM sys.index_columns ic JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE i.is_primary_key = 1) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id WHERE o.name = '{objectName.Replace("'", "''", StringComparison.Ordinal)}' ORDER BY c.column_id";

    public override string BuildDdlSql(string objectName, string? schema, string objectType)
        => $"SELECT OBJECT_DEFINITION(OBJECT_ID('{(string.IsNullOrWhiteSpace(schema) ? "dbo" : schema)}.{objectName.Replace("'", "''", StringComparison.Ordinal)}')) AS ddl";

    public override string BuildPagedSelectSql(string objectName, string? schema, int pageIndex, int pageSize)
    {
        var offset = Math.Max(0, pageIndex - 1) * Math.Clamp(pageSize, 1, 100);
        return $"SELECT * FROM {QualifiedName(objectName, schema)} ORDER BY (SELECT 1) OFFSET {offset} ROWS FETCH NEXT {Math.Clamp(pageSize, 1, 100)} ROWS ONLY";
    }

    public override string LimitSelectSql(string selectSql, int limit)
        => $"SELECT TOP {Math.Clamp(limit, 1, 100)} * FROM ({selectSql.Trim().TrimEnd(';')}) atlas_preview";
}

public class OracleDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "Oracle";

    protected override IReadOnlySet<string> SupportedDataTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NUMBER",
        "VARCHAR2",
        "NVARCHAR2",
        "CLOB",
        "DATE",
        "TIMESTAMP"
    };

    public override string BuildListObjectsSql(string objectType)
    {
        var type = objectType.Trim().ToLowerInvariant();
        return type switch
        {
            "view" => "SELECT VIEW_NAME AS name, 'view' AS object_type, USER AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM USER_VIEWS ORDER BY VIEW_NAME",
            "procedure" => "SELECT OBJECT_NAME AS name, 'procedure' AS object_type, USER AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, CREATED AS created_at, LAST_DDL_TIME AS updated_at FROM USER_OBJECTS WHERE OBJECT_TYPE = 'PROCEDURE' ORDER BY OBJECT_NAME",
            "trigger" => "SELECT TRIGGER_NAME AS name, 'trigger' AS object_type, OWNER AS schema_name, NULL AS engine, NULL AS row_count, DESCRIPTION AS comment, NULL AS created_at, NULL AS updated_at FROM USER_TRIGGERS ORDER BY TRIGGER_NAME",
            _ => "SELECT TABLE_NAME AS name, 'table' AS object_type, USER AS schema_name, NULL AS engine, NUM_ROWS AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM USER_TABLES ORDER BY TABLE_NAME"
        };
    }

    public override string BuildColumnsSql(string objectName, string? schema)
        => $"SELECT COLUMN_NAME AS name, DATA_TYPE AS data_type, DATA_TYPE AS raw_data_type, DATA_LENGTH AS length, DATA_PRECISION AS precision, DATA_SCALE AS scale, NULLABLE AS nullable, DATA_DEFAULT AS default_value, COLUMN_ID AS ordinal FROM USER_TAB_COLUMNS WHERE TABLE_NAME = UPPER('{objectName.Replace("'", "''", StringComparison.Ordinal)}') ORDER BY COLUMN_ID";

    public override string BuildDdlSql(string objectName, string? schema, string objectType)
        => $"SELECT DBMS_METADATA.GET_DDL('{(string.Equals(objectType, "view", StringComparison.OrdinalIgnoreCase) ? "VIEW" : "TABLE")}', UPPER('{objectName.Replace("'", "''", StringComparison.Ordinal)}')) AS ddl FROM DUAL";

    public override string BuildPagedSelectSql(string objectName, string? schema, int pageIndex, int pageSize)
    {
        var offset = Math.Max(0, pageIndex - 1) * Math.Clamp(pageSize, 1, 100);
        return $"SELECT * FROM {QualifiedName(objectName, schema)} OFFSET {offset} ROWS FETCH NEXT {Math.Clamp(pageSize, 1, 100)} ROWS ONLY";
    }

    public override string LimitSelectSql(string selectSql, int limit)
        => $"SELECT * FROM ({selectSql.Trim().TrimEnd(';')}) atlas_preview FETCH FIRST {Math.Clamp(limit, 1, 100)} ROWS ONLY";
}

public sealed class DmDatabaseDialect : OracleDatabaseDialect
{
    public override string DriverCode => "Dm";
}

public sealed class KingbaseDatabaseDialect : PostgreSqlDatabaseDialect
{
    public override string DriverCode => "Kdbndp";
}

public sealed class OscarDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "Oscar";

    public override string BuildListObjectsSql(string objectType)
    {
        var type = objectType.Trim().ToLowerInvariant();
        return type switch
        {
            "view" => "SELECT table_name AS name, 'view' AS object_type, table_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.views ORDER BY table_schema, table_name",
            "procedure" => "SELECT routine_name AS name, 'procedure' AS object_type, routine_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.routines WHERE routine_type = 'PROCEDURE' ORDER BY routine_schema, routine_name",
            "trigger" => "SELECT trigger_name AS name, 'trigger' AS object_type, trigger_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.triggers ORDER BY trigger_schema, trigger_name",
            _ => "SELECT table_name AS name, 'table' AS object_type, table_schema AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, NULL AS created_at, NULL AS updated_at FROM information_schema.tables WHERE table_type = 'BASE TABLE' ORDER BY table_schema, table_name"
        };
    }

    public override string BuildColumnsSql(string objectName, string? schema)
        => $"SELECT column_name AS name, data_type AS data_type, data_type AS raw_data_type, character_maximum_length AS length, numeric_precision AS precision, numeric_scale AS scale, is_nullable AS nullable, column_default AS default_value, ordinal_position AS ordinal FROM information_schema.columns WHERE table_name = '{objectName.Replace("'", "''", StringComparison.Ordinal)}' ORDER BY ordinal_position";

    public override string BuildDdlSql(string objectName, string? schema, string objectType)
        => $"SELECT '' AS ddl WHERE 1 = 0";
}
