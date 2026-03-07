using System.Globalization;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.DynamicTables;

internal static class DynamicSqlBuilder
{
    public const string TenantColumnName = "TenantIdValue";

    public static List<DbColumnInfo> BuildCreateTableColumns(IReadOnlyList<DynamicField> fields)
    {
        var columns = new List<DbColumnInfo>
        {
            new()
            {
                DbColumnName = TenantColumnName,
                DataType = "TEXT",
                IsNullable = false,
                IsPrimarykey = false,
                IsIdentity = false
            }
        };

        foreach (var field in fields)
        {
            var column = BuildDbColumnInfo(field);
            if (field.IsPrimaryKey && field.IsAutoIncrement)
            {
                column.IsIdentity = true;
            }

            columns.Add(column);
        }

        return columns;
    }

    public static IReadOnlyList<DynamicIndexSpec> BuildCreateIndexSpecs(DynamicTable table, IReadOnlyList<DynamicIndex> indexes)
    {
        if (indexes.Count == 0)
        {
            return Array.Empty<DynamicIndexSpec>();
        }

        var specs = new List<DynamicIndexSpec>(indexes.Count);
        foreach (var index in indexes)
        {
            var fields = System.Text.Json.JsonSerializer.Deserialize<string[]>(index.FieldsJson) ?? Array.Empty<string>();
            if (fields.Length == 0)
            {
                continue;
            }

            specs.Add(new DynamicIndexSpec(index.Name, fields, index.IsUnique));
        }

        return specs;
    }

    public static DbColumnInfo BuildAddColumnInfo(DynamicField field)
    {
        return BuildDbColumnInfo(field);
    }

    public static string BuildTableName(DynamicTable table)
    {
        return table.TableKey;
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

    private static DbColumnInfo BuildDbColumnInfo(DynamicField field)
    {
        return new DbColumnInfo
        {
            DbColumnName = field.Name,
            DataType = MapToSqlType(field, DynamicDbType.Sqlite),
            IsNullable = field.AllowNull,
            IsPrimarykey = field.IsPrimaryKey,
            IsIdentity = field.IsAutoIncrement,
            Length = field.Length ?? 0,
            DecimalDigits = field.Scale ?? 0,
            DefaultValue = BuildDefaultValueLiteral(field)
        };
    }

    private static string? BuildDefaultValueLiteral(DynamicField field)
    {
        if (string.IsNullOrWhiteSpace(field.DefaultValue))
        {
            return null;
        }

        var value = field.DefaultValue.Trim();
        return field.FieldType switch
        {
            DynamicFieldType.Int => TryParseInt(value, out var i) ? i.ToString(CultureInfo.InvariantCulture) : throw new BusinessException($"字段 {field.Name} 的默认值 '{value}' 不是有效的整数。", ErrorCodes.ValidationError),
            DynamicFieldType.Long => TryParseLong(value, out var l) ? l.ToString(CultureInfo.InvariantCulture) : throw new BusinessException($"字段 {field.Name} 的默认值 '{value}' 不是有效的长整数。", ErrorCodes.ValidationError),
            DynamicFieldType.Decimal => TryParseDecimal(value, out var d) ? d.ToString(CultureInfo.InvariantCulture) : throw new BusinessException($"字段 {field.Name} 的默认值 '{value}' 不是有效的小数。", ErrorCodes.ValidationError),
            DynamicFieldType.Bool => value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "1" :
                value.Equals("false", StringComparison.OrdinalIgnoreCase) ? "0" :
                throw new BusinessException($"字段 {field.Name} 的默认值 '{value}' 不是有效的布尔值（应为 true 或 false）。", ErrorCodes.ValidationError),
            _ => value
        };
    }

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseLong(string value, out long result) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

}

internal sealed record DynamicIndexSpec(string IndexName, IReadOnlyList<string> Fields, bool IsUnique);
