using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// D3：基于 TableSchema（JSON 数组，每项含 name + type）将记录数据 JSON 强制类型化。
/// 支持类型：string / number / integer / boolean / date / json / array。
/// 老数据 / 缺失字段：保留原值。
/// </summary>
public static class AiDatabaseValueCoercer
{
    /// <summary>解析 schema 列定义。允许 schemaJson 为空——返回空集合（兼容旧库）。</summary>
    public static IReadOnlyList<AiDatabaseColumnDefinition> ParseColumns(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return Array.Empty<AiDatabaseColumnDefinition>();
        }

        try
        {
            var node = JsonNode.Parse(schemaJson);
            if (node is not JsonArray array)
            {
                return Array.Empty<AiDatabaseColumnDefinition>();
            }

            var list = new List<AiDatabaseColumnDefinition>(array.Count);
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }
                var name = obj["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                var typeText = obj["type"]?.GetValue<string>();
                var required = obj["required"]?.GetValue<bool>() ?? false;
                list.Add(new AiDatabaseColumnDefinition(name.Trim(), ParseFieldType(typeText), required));
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<AiDatabaseColumnDefinition>();
        }
    }

    /// <summary>按 schema 强制类型化记录 JSON；非法值抛 BusinessException。</summary>
    public static string Coerce(string? schemaJson, string dataJson)
    {
        var columns = ParseColumns(schemaJson);
        if (columns.Count == 0)
        {
            return string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson);
        }
        catch (JsonException)
        {
            throw new BusinessException("记录数据不是合法 JSON。", ErrorCodes.ValidationError);
        }
        if (root is not JsonObject record)
        {
            throw new BusinessException("记录数据必须是 JSON 对象。", ErrorCodes.ValidationError);
        }

        foreach (var column in columns)
        {
            var lookup = FindCaseInsensitive(record, column.Name);
            if (!lookup.HasValue)
            {
                if (column.Required)
                {
                    throw new BusinessException($"必填字段 {column.Name} 缺失。", ErrorCodes.ValidationError);
                }
                continue;
            }

            var (existingKey, existingNode) = lookup.Value;
            var coerced = CoerceValue(column, existingNode);
            record.Remove(existingKey);
            record[column.Name] = coerced;
        }

        return record.ToJsonString();
    }

    private static AiDatabaseFieldType ParseFieldType(string? typeText)
    {
        if (string.IsNullOrWhiteSpace(typeText))
        {
            return AiDatabaseFieldType.String;
        }
        return typeText.Trim().ToLowerInvariant() switch
        {
            "string" or "text" => AiDatabaseFieldType.String,
            "number" or "double" or "float" or "decimal" => AiDatabaseFieldType.Number,
            "integer" or "int" or "long" => AiDatabaseFieldType.Integer,
            "boolean" or "bool" => AiDatabaseFieldType.Boolean,
            "date" or "datetime" => AiDatabaseFieldType.Date,
            "array" => AiDatabaseFieldType.Array,
            "json" or "object" => AiDatabaseFieldType.Json,
            _ => AiDatabaseFieldType.String
        };
    }

    private static (string Key, JsonNode? Node)? FindCaseInsensitive(JsonObject record, string columnName)
    {
        foreach (var pair in record)
        {
            if (string.Equals(pair.Key, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return (pair.Key, pair.Value);
            }
        }
        return null;
    }

    private static JsonNode? CoerceValue(AiDatabaseColumnDefinition column, JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        try
        {
            return column.Type switch
            {
                AiDatabaseFieldType.String => JsonValue.Create(node is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : node.ToJsonString().Trim('"')),
                AiDatabaseFieldType.Number => JsonValue.Create(ToDouble(node)),
                AiDatabaseFieldType.Integer => JsonValue.Create(ToInt64(node)),
                AiDatabaseFieldType.Boolean => JsonValue.Create(ToBoolean(node)),
                AiDatabaseFieldType.Date => JsonValue.Create(ToIsoDate(node)),
                AiDatabaseFieldType.Array => node is JsonArray ? node : throw new BusinessException($"字段 {column.Name} 必须为数组。", ErrorCodes.ValidationError),
                AiDatabaseFieldType.Json => node is JsonObject ? node : throw new BusinessException($"字段 {column.Name} 必须为对象。", ErrorCodes.ValidationError),
                _ => node
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new BusinessException($"字段 {column.Name} 类型转换失败。", ErrorCodes.ValidationError);
        }
    }

    private static double ToDouble(JsonNode node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<double>(out var d)) return d;
            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
        }
        throw new BusinessException("数值字段格式不合法。", ErrorCodes.ValidationError);
    }

    private static long ToInt64(JsonNode node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<double>(out var d) && d == Math.Floor(d)) return (long)d;
            if (jv.TryGetValue<string>(out var s) && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
        }
        throw new BusinessException("整数字段格式不合法。", ErrorCodes.ValidationError);
    }

    private static bool ToBoolean(JsonNode node)
    {
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var b)) return b;
            if (jv.TryGetValue<string>(out var s) && bool.TryParse(s, out var v)) return v;
            if (jv.TryGetValue<long>(out var l)) return l != 0;
        }
        throw new BusinessException("布尔字段格式不合法。", ErrorCodes.ValidationError);
    }

    private static string ToIsoDate(JsonNode node)
    {
        if (node is JsonValue jv && jv.TryGetValue<string>(out var s))
        {
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            }
        }
        throw new BusinessException("日期字段格式不合法。", ErrorCodes.ValidationError);
    }
}

public sealed record AiDatabaseColumnDefinition(string Name, AiDatabaseFieldType Type, bool Required);

public enum AiDatabaseFieldType
{
    String = 0,
    Number = 1,
    Integer = 2,
    Boolean = 3,
    Date = 4,
    Json = 5,
    Array = 6
}
