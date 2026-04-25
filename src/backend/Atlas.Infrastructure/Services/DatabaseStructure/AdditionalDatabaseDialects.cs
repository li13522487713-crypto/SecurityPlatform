namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed class SqlServerDatabaseDialect : DatabaseDialectBase
{
    public override string DriverCode => "SqlServer";

    public override string BuildListObjectsSql(string objectType)
        => "SELECT name, CASE WHEN type = 'V' THEN 'view' WHEN type = 'P' THEN 'procedure' WHEN type = 'TR' THEN 'trigger' ELSE 'table' END AS object_type, SCHEMA_NAME(schema_id) AS schema_name, NULL AS engine, NULL AS row_count, NULL AS comment, create_date AS created_at, modify_date AS updated_at FROM sys.objects WHERE type IN ('U','V','P','TR') ORDER BY name";

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

public sealed class OracleDatabaseDialect : PostgreSqlDatabaseDialect
{
    public override string DriverCode => "Oracle";
}

public sealed class DmDatabaseDialect : PostgreSqlDatabaseDialect
{
    public override string DriverCode => "Dm";
}

public sealed class KingbaseDatabaseDialect : PostgreSqlDatabaseDialect
{
    public override string DriverCode => "Kdbndp";
}

public sealed class OscarDatabaseDialect : PostgreSqlDatabaseDialect
{
    public override string DriverCode => "Oscar";
}
