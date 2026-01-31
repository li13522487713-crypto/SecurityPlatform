using System.Globalization;
using System.Text;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Infrastructure.DynamicTables;

internal static class DynamicSqlBuilder
{
    public static string BuildCreateTableSql(DynamicTable table, IReadOnlyList<DynamicField> fields)
    {
        var dbType = table.DbType;
        var tableName = Quote(table.TableKey, dbType);

        var columnDefs = new List<string>();
        string? primaryKeyColumn = null;

        foreach (var field in fields)
        {
            var columnName = Quote(field.Name, dbType);
            if (field.IsPrimaryKey && field.IsAutoIncrement && dbType == DynamicDbType.Sqlite)
            {
                columnDefs.Add($"{columnName} INTEGER PRIMARY KEY AUTOINCREMENT");
                primaryKeyColumn = field.Name;
                continue;
            }

            var typeSql = MapToSqlType(field, dbType);
            var nullSql = field.AllowNull ? string.Empty : " NOT NULL";
            columnDefs.Add($"{columnName} {typeSql}{nullSql}");
            if (field.IsPrimaryKey)
            {
                primaryKeyColumn = field.Name;
            }
        }

        if (!string.IsNullOrWhiteSpace(primaryKeyColumn))
        {
            var hasInlinePk = columnDefs.Any(def => def.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase));
            if (!hasInlinePk)
            {
                columnDefs.Add($"PRIMARY KEY ({Quote(primaryKeyColumn, dbType)})");
            }
        }

        foreach (var uniqueField in fields.Where(x => x.IsUnique))
        {
            columnDefs.Add($"UNIQUE ({Quote(uniqueField.Name, dbType)})");
        }

        var sql = new StringBuilder();
        sql.Append("CREATE TABLE ");
        sql.Append(tableName);
        sql.Append(" (");
        sql.Append(string.Join(", ", columnDefs));
        sql.Append(");");

        return sql.ToString();
    }

    public static string BuildCreateIndexSql(DynamicTable table, IReadOnlyList<string> fields, string indexName, bool isUnique)
    {
        var dbType = table.DbType;
        var quotedFields = fields.Select(f => Quote(f, dbType));
        var uniqueSql = isUnique ? "UNIQUE " : string.Empty;
        return $"CREATE {uniqueSql}INDEX {Quote(indexName, dbType)} ON {Quote(table.TableKey, dbType)} ({string.Join(", ", quotedFields)});";
    }

    public static string BuildDropTableSql(DynamicTable table)
    {
        return $"DROP TABLE IF EXISTS {Quote(table.TableKey, table.DbType)};";
    }

    public static string Quote(string identifier, DynamicDbType dbType)
    {
        return dbType switch
        {
            DynamicDbType.SqlServer => $"[{identifier}]",
            DynamicDbType.MySql => $"`{identifier}`",
            DynamicDbType.PostgreSql => $"\"{identifier}\"",
            _ => $"\"{identifier}\""
        };
    }

    public static string MapToSqlType(DynamicField field, DynamicDbType dbType)
    {
        return field.FieldType switch
        {
            DynamicFieldType.Int => "INTEGER",
            DynamicFieldType.Long => "INTEGER",
            DynamicFieldType.Decimal => BuildDecimal(field),
            DynamicFieldType.String => "TEXT",
            DynamicFieldType.Text => "TEXT",
            DynamicFieldType.Bool => "INTEGER",
            DynamicFieldType.DateTime => "TEXT",
            DynamicFieldType.Date => "TEXT",
            _ => "TEXT"
        };
    }

    private static string BuildDecimal(DynamicField field)
    {
        var precision = field.Precision ?? 18;
        var scale = field.Scale ?? 2;
        precision = Math.Clamp(precision, 1, 38);
        scale = Math.Clamp(scale, 0, 18);
        if (scale > precision)
        {
            scale = precision;
        }

        return string.Create(CultureInfo.InvariantCulture, $"NUMERIC({precision},{scale})");
    }
}
