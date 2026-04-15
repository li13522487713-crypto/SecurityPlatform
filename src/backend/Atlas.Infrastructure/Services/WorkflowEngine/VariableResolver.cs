using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

/// <summary>
/// 工作流变量解析器：提供模板渲染、路径解析、字面量转换与条件求值。
/// </summary>
public static class VariableResolver
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*(?<path>[^{}]+?)\s*\}\}", RegexOptions.Compiled);
    private static readonly JsonElement NullElement = JsonDocument.Parse("null").RootElement.Clone();

    public static string RenderTemplate(string template, IReadOnlyDictionary<string, JsonElement> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var path = match.Groups["path"].Value.Trim();
            return TryResolvePath(variables, path, out var resolved)
                ? ToDisplayText(resolved)
                : string.Empty;
        });
    }

    public static bool TryResolvePath(
        IReadOnlyDictionary<string, JsonElement> variables,
        string path,
        out JsonElement value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = ParsePathSegments(path);
        if (segments.Count == 0)
        {
            return false;
        }

        if (!variables.TryGetValue(segments[0].PropertyName, out var current))
        {
            return false;
        }

        if (!ApplyIndexes(ref current, segments[0].Indexes))
        {
            return false;
        }

        for (var i = 1; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (!TryGetPropertyValue(current, segment.PropertyName, out current))
            {
                return false;
            }

            if (!ApplyIndexes(ref current, segment.Indexes))
            {
                return false;
            }
        }

        value = current;
        return true;
    }

    public static JsonElement ParseLiteralOrTemplate(string value, IReadOnlyDictionary<string, JsonElement> variables)
    {
        var rendered = RenderTemplate(value ?? string.Empty, variables);
        return ParseLiteral(rendered);
    }

    public static JsonElement ParseLiteral(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return CreateStringElement(string.Empty);
        }

        if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
        {
            return NullElement;
        }

        if (bool.TryParse(trimmed, out var boolValue))
        {
            return JsonSerializer.SerializeToElement(boolValue);
        }

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return JsonSerializer.SerializeToElement(longValue);
        }

        if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return JsonSerializer.SerializeToElement(decimalValue);
        }

        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')) ||
            (trimmed.StartsWith('"') && trimmed.EndsWith('"')))
        {
            try
            {
                return JsonDocument.Parse(trimmed).RootElement.Clone();
            }
            catch
            {
                // fallback to plain string when JSON parsing fails.
            }
        }

        return CreateStringElement(trimmed);
    }

    public static string ToDisplayText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            JsonValueKind.Array => value.GetRawText(),
            JsonValueKind.Object => value.GetRawText(),
            _ => value.ToString()
        };
    }

    public static bool EvaluateCondition(string condition, IReadOnlyDictionary<string, JsonElement> variables)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var orParts = SplitLogical(condition, "||");
        if (orParts.Count > 1)
        {
            return orParts.Any(part => EvaluateAndExpression(part, variables));
        }

        return EvaluateAndExpression(condition, variables);
    }

    public static bool IsTruthy(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => false,
            JsonValueKind.Undefined => false,
            JsonValueKind.Number => TryGetNumber(value, out var number) && Math.Abs(number) > double.Epsilon,
            JsonValueKind.String => IsTruthyString(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => false
        };
    }

    public static bool TryGetBoolean(JsonElement value, out bool result)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                result = true;
                return true;
            case JsonValueKind.False:
                result = false;
                return true;
            case JsonValueKind.String:
                if (bool.TryParse(value.GetString(), out result))
                {
                    return true;
                }

                var normalized = (value.GetString() ?? string.Empty).Trim();
                if (string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return true;
                }

                if (string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    return true;
                }

                break;
            case JsonValueKind.Number:
                if (TryGetNumber(value, out var numeric))
                {
                    result = Math.Abs(numeric) > double.Epsilon;
                    return true;
                }

                break;
        }

        result = false;
        return false;
    }

    public static string GetConfigString(
        IReadOnlyDictionary<string, JsonElement> config,
        string key,
        string defaultValue = "")
    {
        if (!TryGetConfigValue(config, key, out var raw))
        {
            return defaultValue;
        }

        var text = ToDisplayText(raw);
        if (string.IsNullOrWhiteSpace(text))
        {
            return defaultValue;
        }

        var decoded = WebUtility.HtmlDecode(text);
        return string.IsNullOrWhiteSpace(decoded) ? defaultValue : decoded;
    }

    public static int GetConfigInt32(
        IReadOnlyDictionary<string, JsonElement> config,
        string key,
        int defaultValue = 0)
    {
        if (!TryGetConfigValue(config, key, out var raw))
        {
            return defaultValue;
        }

        if (raw.ValueKind == JsonValueKind.Number && raw.TryGetInt32(out var intFromNumber))
        {
            return intFromNumber;
        }

        var text = ToDisplayText(raw);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
            ? intValue
            : defaultValue;
    }

    public static long GetConfigInt64(
        IReadOnlyDictionary<string, JsonElement> config,
        string key,
        long defaultValue = 0L)
    {
        if (!TryGetConfigValue(config, key, out var raw))
        {
            return defaultValue;
        }

        if (raw.ValueKind == JsonValueKind.Number && raw.TryGetInt64(out var longFromNumber))
        {
            return longFromNumber;
        }

        var text = ToDisplayText(raw);
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue)
            ? longValue
            : defaultValue;
    }

    public static bool GetConfigBoolean(
        IReadOnlyDictionary<string, JsonElement> config,
        string key,
        bool defaultValue = false)
    {
        if (!TryGetConfigValue(config, key, out var raw))
        {
            return defaultValue;
        }

        if (TryGetBoolean(raw, out var fromRaw))
        {
            return fromRaw;
        }

        var text = ToDisplayText(raw);
        return bool.TryParse(text, out var parsed)
            ? parsed
            : defaultValue;
    }

    public static bool TryGetConfigValue(
        IReadOnlyDictionary<string, JsonElement> config,
        string key,
        out JsonElement value)
    {
        if (config.TryGetValue(key, out value))
        {
            return true;
        }

        if (TryGetConfigValueByPath(config, key, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public static Dictionary<string, JsonElement> ParseVariableDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                map[property.Name] = property.Value.Clone();
            }

            return map;
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static JsonElement CreateStringElement(string value)
    {
        return JsonSerializer.SerializeToElement(value ?? string.Empty);
    }

    private static bool TryGetConfigValueByPath(
        IReadOnlyDictionary<string, JsonElement> config,
        string key,
        out JsonElement value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(key) || !key.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (!config.TryGetValue(segments[0], out var current))
        {
            return false;
        }

        for (var index = 1; index < segments.Length; index++)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !TryGetPropertyValue(current, segments[index], out current))
            {
                return false;
            }
        }

        value = current;
        return true;
    }

    private static bool EvaluateAndExpression(string expression, IReadOnlyDictionary<string, JsonElement> variables)
    {
        var andParts = SplitLogical(expression, "&&");
        return andParts.All(part => EvaluateSingleCondition(part, variables));
    }

    private static bool EvaluateSingleCondition(string expression, IReadOnlyDictionary<string, JsonElement> variables)
    {
        var trimmed = expression.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed.StartsWith('!'))
        {
            return !EvaluateSingleCondition(trimmed[1..], variables);
        }

        var operatorResult = TryExtractOperator(trimmed);
        if (operatorResult is null)
        {
            if (TryResolveOperand(trimmed, variables, out var truthyOperand))
            {
                return IsTruthy(truthyOperand);
            }

            return IsTruthy(ParseLiteral(trimmed));
        }

        var (leftText, op, rightText) = operatorResult.Value;
        if (!TryResolveOperand(leftText, variables, out var left))
        {
            left = ParseLiteral(leftText);
        }

        if (!TryResolveOperand(rightText, variables, out var right))
        {
            right = ParseLiteral(rightText);
        }

        return op switch
        {
            "==" => CompareEquals(left, right),
            "!=" => !CompareEquals(left, right),
            ">" => CompareNumeric(left, right, (l, r) => l > r),
            "<" => CompareNumeric(left, right, (l, r) => l < r),
            ">=" => CompareNumeric(left, right, (l, r) => l >= r),
            "<=" => CompareNumeric(left, right, (l, r) => l <= r),
            "contains" => ToDisplayText(left).Contains(ToDisplayText(right), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryResolveOperand(
        string operandText,
        IReadOnlyDictionary<string, JsonElement> variables,
        out JsonElement value)
    {
        var normalized = operandText.Trim();
        if (normalized.StartsWith("{{", StringComparison.Ordinal) &&
            normalized.EndsWith("}}", StringComparison.Ordinal) &&
            normalized.Length > 4)
        {
            var path = normalized[2..^2].Trim();
            return TryResolvePath(variables, path, out value);
        }

        return TryResolvePath(variables, normalized, out value);
    }

    private static bool CompareEquals(JsonElement left, JsonElement right)
    {
        if (TryGetBoolean(left, out var leftBool) && TryGetBoolean(right, out var rightBool))
        {
            return leftBool == rightBool;
        }

        if (TryGetNumber(left, out var leftNumber) && TryGetNumber(right, out var rightNumber))
        {
            return Math.Abs(leftNumber - rightNumber) <= 0.0000001d;
        }

        var leftText = ToDisplayText(left);
        var rightText = ToDisplayText(right);
        return string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNumeric(JsonElement left, JsonElement right, Func<double, double, bool> comparator)
    {
        if (!TryGetNumber(left, out var leftNumber) || !TryGetNumber(right, out var rightNumber))
        {
            return false;
        }

        return comparator(leftNumber, rightNumber);
    }

    private static bool TryGetNumber(JsonElement element, out double value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDouble(out value);
            case JsonValueKind.String:
                return double.TryParse(
                    element.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
            case JsonValueKind.True:
                value = 1d;
                return true;
            case JsonValueKind.False:
                value = 0d;
                return true;
            default:
                value = 0d;
                return false;
        }
    }

    private static bool IsTruthyString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static (string Left, string Operator, string Right)? TryExtractOperator(string expression)
    {
        var match = Regex.Match(
            expression,
            @"^(?<left>.+?)\s*(?<op>==|!=|>=|<=|>|<|contains)\s*(?<right>.+)$",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        return (
            match.Groups["left"].Value.Trim(),
            match.Groups["op"].Value.Trim().ToLowerInvariant(),
            match.Groups["right"].Value.Trim());
    }

    private static List<string> SplitLogical(string expression, string separator)
    {
        var parts = new List<string>();
        var start = 0;
        var quote = '\0';

        for (var i = 0; i <= expression.Length - separator.Length; i++)
        {
            var current = expression[i];
            if (quote == '\0' && (current == '"' || current == '\''))
            {
                quote = current;
                continue;
            }

            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (string.Compare(expression, i, separator, 0, separator.Length, StringComparison.Ordinal) == 0)
            {
                parts.Add(expression[start..i].Trim());
                i += separator.Length - 1;
                start = i + 1;
            }
        }

        parts.Add(expression[start..].Trim());
        return parts.Where(static x => x.Length > 0).ToList();
    }

    private static List<PathSegment> ParsePathSegments(string path)
    {
        var rawSegments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var segments = new List<PathSegment>(rawSegments.Length);
        foreach (var raw in rawSegments)
        {
            var propertyName = raw;
            var indexes = new List<int>();

            var bracketStart = raw.IndexOf('[');
            if (bracketStart >= 0)
            {
                propertyName = raw[..bracketStart];
                var remain = raw[bracketStart..];
                while (remain.Length > 0)
                {
                    if (!remain.StartsWith("[", StringComparison.Ordinal))
                    {
                        return new List<PathSegment>();
                    }

                    var close = remain.IndexOf(']');
                    if (close <= 1)
                    {
                        return new List<PathSegment>();
                    }

                    var indexText = remain[1..close];
                    if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0)
                    {
                        return new List<PathSegment>();
                    }

                    indexes.Add(index);
                    remain = remain[(close + 1)..];
                }
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return new List<PathSegment>();
            }

            segments.Add(new PathSegment(propertyName.Trim(), indexes));
        }

        return segments;
    }

    private static bool ApplyIndexes(ref JsonElement current, IReadOnlyList<int> indexes)
    {
        foreach (var index in indexes)
        {
            if (current.ValueKind != JsonValueKind.Array || current.GetArrayLength() <= index)
            {
                return false;
            }

            current = current[index];
        }

        return true;
    }

    private static bool TryGetPropertyValue(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value;
            return true;
        }

        return false;
    }

    private readonly record struct PathSegment(string PropertyName, IReadOnlyList<int> Indexes);
}
