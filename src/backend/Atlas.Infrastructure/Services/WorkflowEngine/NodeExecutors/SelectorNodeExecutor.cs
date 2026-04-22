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

        if (context.Node.Config.TryGetValue("inputs", out var inputsVal) &&
            inputsVal.ValueKind == JsonValueKind.Object &&
            inputsVal.TryGetProperty("branches", out var branchesArr) &&
            branchesArr.ValueKind == JsonValueKind.Array)
        {
            int branchIndex = 0;
            foreach (var branch in branchesArr.EnumerateArray())
            {
                if (EvaluateBranch(branch, context))
                {
                    var portId = branchIndex == 0 ? "true" : $"true_{branchIndex}";
                    outputs["selected_branch"] = VariableResolver.CreateStringElement(portId);
                    outputs["selector_result"] = VariableResolver.CreateStringElement(portId);
                    return Task.FromResult(new NodeExecutionResult(true, outputs));
                }
                branchIndex++;
            }
            outputs["selected_branch"] = VariableResolver.CreateStringElement("false");
            outputs["selector_result"] = VariableResolver.CreateStringElement("false");
            return Task.FromResult(new NodeExecutionResult(true, outputs));
        }

        var condition = context.GetConfigString("condition");
        if (string.IsNullOrWhiteSpace(condition))
        {
            condition = BuildConditionExpressionFromStructuredConfig(context.Node.Config);
        }

        var result = context.EvaluateCondition(condition);
        outputs["selector_result"] = JsonSerializer.SerializeToElement(result);
        outputs["selected_branch"] = VariableResolver.CreateStringElement(result ? "true" : "false");

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }

    private static bool EvaluateBranch(JsonElement branch, NodeExecutionContext context)
    {
        if (!branch.TryGetProperty("condition", out var condObj) || condObj.ValueKind != JsonValueKind.Object)
            return false;

        int logic = 2; 
        if (condObj.TryGetProperty("logic", out var logicProp) && logicProp.ValueKind == JsonValueKind.Number)
        {
            logic = logicProp.GetInt32();
        }

        if (!condObj.TryGetProperty("conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Array)
            return false;

        bool result = logic == 2; 

        foreach (var item in conditions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            bool itemResult = EvaluateConditionItem(item, context);
            if (logic == 2) 
            {
                result = result && itemResult;
                if (!result) break; 
            }
            else 
            {
                result = result || itemResult;
                if (result) break; 
            }
        }
        return result;
    }

    private static bool EvaluateConditionItem(JsonElement item, NodeExecutionContext context)
    {
        int op = 1;
        if (item.TryGetProperty("operator", out var opProp) && opProp.ValueKind == JsonValueKind.Number)
        {
            op = opProp.GetInt32();
        }

        var leftVal = GetValueExpressionResult(item, "left", context);
        var rightVal = GetValueExpressionResult(item, "right", context);

        return CompareValues(leftVal, op, rightVal);
    }

    private static JsonElement? GetValueExpressionResult(JsonElement item, string propertyName, NodeExecutionContext context)
    {
        if (!item.TryGetProperty(propertyName, out var exprObj) || exprObj.ValueKind != JsonValueKind.Object)
            return null;

        if (!exprObj.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
            return null;

        string type = typeProp.GetString() ?? "";
        if (type == "ref")
        {
            if (exprObj.TryGetProperty("content", out var contentObj) &&
                contentObj.ValueKind == JsonValueKind.Object &&
                contentObj.TryGetProperty("keyPath", out var keyPathArr) &&
                keyPathArr.ValueKind == JsonValueKind.Array)
            {
                var paths = new List<string>();
                foreach (var k in keyPathArr.EnumerateArray())
                {
                    var part = k.ValueKind == JsonValueKind.String ? k.GetString() : k.GetRawText();
                    if (!string.IsNullOrWhiteSpace(part)) paths.Add(part);
                }

                string fullPath = string.Join(".", paths);
                if (context.Variables.TryGetValue(fullPath, out var val))
                    return val;
            }
        }
        else if (type == "literal")
        {
            if (exprObj.TryGetProperty("content", out var contentProp))
            {
                if (contentProp.ValueKind == JsonValueKind.String)
                {
                    string rawString = contentProp.GetString() ?? "";
                    if (rawString.Contains("{{"))
                    {
                        var evaluated = context.EvaluateExpression(rawString);
                        return evaluated;
                    }
                }
                return contentProp;
            }
        }

        return null;
    }

    private static bool CompareValues(JsonElement? left, int op, JsonElement? right)
    {
        var leftStr = left.HasValue ? VariableResolver.ToDisplayText(left.Value) : string.Empty;
        var rightStr = right.HasValue ? VariableResolver.ToDisplayText(right.Value) : string.Empty;

        bool isLeftNum = double.TryParse(leftStr, out double leftNum);
        bool isRightNum = double.TryParse(rightStr, out double rightNum);

        return op switch
        {
            1 => string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase), // Equal
            2 => !string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase), // NotEqual
            3 => leftStr.Length > rightStr.Length, // LengthGt
            4 => leftStr.Length >= rightStr.Length, // LengthGtEqual
            5 => leftStr.Length < rightStr.Length, // LengthLt
            6 => leftStr.Length <= rightStr.Length, // LengthLtEqual
            7 => leftStr.Contains(rightStr, StringComparison.OrdinalIgnoreCase), // Contains
            8 => !leftStr.Contains(rightStr, StringComparison.OrdinalIgnoreCase), // NotContains
            9 => string.IsNullOrWhiteSpace(leftStr), // Null
            10 => !string.IsNullOrWhiteSpace(leftStr), // NotNull
            11 => string.Equals(leftStr, "true", StringComparison.OrdinalIgnoreCase), // True
            12 => string.Equals(leftStr, "false", StringComparison.OrdinalIgnoreCase), // False
            13 => isLeftNum && isRightNum && leftNum > rightNum, // Gt
            14 => isLeftNum && isRightNum && leftNum >= rightNum, // GtEqual
            15 => isLeftNum && isRightNum && leftNum < rightNum, // Lt
            16 => isLeftNum && isRightNum && leftNum <= rightNum, // LtEqual
            _ => false
        };
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
