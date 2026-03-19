using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 条件分支节点：支持 condition 表达式，或 conditions+logic 结构化条件。
/// </summary>
public sealed class SelectorNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Selector;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var condition = context.GetConfigString("condition");
        if (string.IsNullOrWhiteSpace(condition))
        {
            condition = BuildConditionExpressionFromStructuredConfig(context.Node.Config);
        }

        var result = context.EvaluateCondition(condition);
        outputs["selector_result"] = JsonSerializer.SerializeToElement(result);
        outputs["selected_branch"] = VariableResolver.CreateStringElement(result ? "true_branch" : "false_branch");

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }

    private static string BuildConditionExpressionFromStructuredConfig(
        IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!config.TryGetValue("conditions", out var rawConditions) ||
            rawConditions.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var logic = VariableResolver.GetConfigString(config, "logic", "and")
            .Trim()
            .ToLowerInvariant();
        var separator = logic == "or" ? " || " : " && ";
        var clauses = new List<string>();

        foreach (var condition in rawConditions.EnumerateArray())
        {
            if (condition.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var left = TryGetPropertyText(condition, "left");
            var op = NormalizeOperator(TryGetPropertyText(condition, "op"));
            var right = TryFormatRightOperand(condition);
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(right))
            {
                continue;
            }

            clauses.Add($"{left} {op} {right}");
        }

        return clauses.Count == 0
            ? string.Empty
            : string.Join(separator, clauses);
    }

    private static string NormalizeOperator(string? op)
    {
        return (op ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "eq" => "==",
            "ne" => "!=",
            "gt" => ">",
            "lt" => "<",
            "ge" => ">=",
            "le" => "<=",
            "contains" => "contains",
            "==" or "!=" or ">" or "<" or ">=" or "<=" => (op ?? string.Empty).Trim(),
            _ => string.Empty
        };
    }

    private static string TryFormatRightOperand(JsonElement condition)
    {
        if (!TryGetPropertyValue(condition, "right", out var right))
        {
            return string.Empty;
        }

        return right.ValueKind switch
        {
            JsonValueKind.Number => right.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.String => FormatStringOperand(right.GetString() ?? string.Empty),
            _ => FormatStringOperand(right.GetRawText())
        };
    }

    private static string FormatStringOperand(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return "\"\"";
        }

        if (normalized.StartsWith("{{", StringComparison.Ordinal) &&
            normalized.EndsWith("}}", StringComparison.Ordinal))
        {
            return normalized;
        }

        if (double.TryParse(normalized, out _) ||
            bool.TryParse(normalized, out _) ||
            string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var escaped = normalized.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string? TryGetPropertyText(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyValue(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static bool TryGetPropertyValue(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
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
}
