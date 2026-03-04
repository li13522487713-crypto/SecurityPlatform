using System.Globalization;
using System.Text.Json;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 条件规则评估器（对齐 AntFlow 的条件评估能力）
/// </summary>
public sealed class ConditionEvaluator
{
    private readonly IApprovalProcessVariableRepository _variableRepository;

    public ConditionEvaluator(IApprovalProcessVariableRepository variableRepository)
    {
        _variableRepository = variableRepository;
    }

    /// <summary>
    /// 评估条件规则（支持单个条件或条件组）
    /// </summary>
    public async Task<bool> EvaluateAsync(
        TenantId tenantId,
        long instanceId,
        string? conditionRuleJson,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(conditionRuleJson))
        {
            return true; // 无条件规则，默认通过
        }

        try
        {
            using var doc = JsonDocument.Parse(conditionRuleJson);
            var root = doc.RootElement;

            // CEL: 直接字符串表达式
            if (root.ValueKind == JsonValueKind.String)
            {
                var expression = root.GetString();
                return await EvaluateCelExpressionAsync(tenantId, instanceId, expression, instanceDataJson, cancellationToken);
            }

            // CEL: 标记对象 { exprType: "cel", expression: "..." }
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("exprType", out var exprTypeProp)
                && string.Equals(exprTypeProp.GetString(), "cel", StringComparison.OrdinalIgnoreCase))
            {
                var expression = root.TryGetProperty("expression", out var expressionProp) ? expressionProp.GetString() : null;
                return await EvaluateCelExpressionAsync(tenantId, instanceId, expression, instanceDataJson, cancellationToken);
            }

            // 检查是否为条件组（有 relationship 字段）
            if (root.TryGetProperty("relationship", out var relationshipProp))
            {
                return await EvaluateConditionGroupAsync(tenantId, instanceId, root, instanceDataJson, cancellationToken);
            }
            else
            {
                // 单个条件
                return await EvaluateSingleConditionAsync(tenantId, instanceId, root, instanceDataJson, cancellationToken);
            }
        }
        catch
        {
            // JSON解析失败，默认不通过
            return false;
        }
    }

    /// <summary>
    /// CEL 子集评估器（支持 && / || / 比较操作，变量格式：form.field）
    /// </summary>
    private async Task<bool> EvaluateCelExpressionAsync(
        TenantId tenantId,
        long instanceId,
        string? expression,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var expr = expression.Trim();
        if (expr.Length > 4096)
        {
            return false;
        }

        // 先按 OR 切分，再按 AND 切分（简化实现，不支持括号嵌套）
        var orSegments = expr.Split("||", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (orSegments.Length == 0)
        {
            return false;
        }

        foreach (var orSegment in orSegments)
        {
            var andSegments = orSegment.Split("&&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (andSegments.Length == 0)
            {
                continue;
            }

            var allPassed = true;
            foreach (var andSegment in andSegments)
            {
                var passed = await EvaluateCelClauseAsync(
                    tenantId,
                    instanceId,
                    andSegment.Trim(),
                    instanceDataJson,
                    cancellationToken);
                if (!passed)
                {
                    allPassed = false;
                    break;
                }
            }

            if (allPassed)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> EvaluateCelClauseAsync(
        TenantId tenantId,
        long instanceId,
        string clause,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        if (string.Equals(clause, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(clause, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 支持 form.field OP value
        var operators = new[] { ">=", "<=", "==", "!=", ">", "<" };
        foreach (var op in operators)
        {
            var idx = clause.IndexOf(op, StringComparison.Ordinal);
            if (idx <= 0)
            {
                continue;
            }

            var left = clause[..idx].Trim();
            var right = clause[(idx + op.Length)..].Trim();
            if (!TryGetFormFieldName(left, out var fieldName))
            {
                return false;
            }

            var actualValue = await GetFieldValueAsync(tenantId, instanceId, fieldName, instanceDataJson, cancellationToken);
            var expectedValue = NormalizeCelLiteral(right);
            return EvaluateOperator(actualValue, op switch
            {
                "==" => "equals",
                "!=" => "notequals",
                ">" => "greaterthan",
                "<" => "lessthan",
                ">=" => "greaterthanorequal",
                "<=" => "lessthanorequal",
                _ => "equals"
            }, expectedValue);
        }

        // 支持 contains: form.field.contains("abc")
        const string containsToken = ".contains(";
        var containsIndex = clause.IndexOf(containsToken, StringComparison.Ordinal);
        if (containsIndex > 0 && clause.EndsWith(")", StringComparison.Ordinal))
        {
            var left = clause[..containsIndex].Trim();
            if (!TryGetFormFieldName(left, out var fieldName))
            {
                return false;
            }
            var right = clause[(containsIndex + containsToken.Length)..^1].Trim();
            var actualValue = await GetFieldValueAsync(tenantId, instanceId, fieldName, instanceDataJson, cancellationToken);
            return EvaluateOperator(actualValue, "contains", NormalizeCelLiteral(right));
        }

        return false;
    }

    private static bool TryGetFormFieldName(string left, out string fieldName)
    {
        fieldName = string.Empty;
        if (!left.StartsWith("form.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        fieldName = left["form.".Length..].Trim();
        return !string.IsNullOrWhiteSpace(fieldName);
    }

    private static string NormalizeCelLiteral(string text)
    {
        if (text.Length >= 2
            && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
        {
            return text[1..^1];
        }
        return text;
    }

    /// <summary>
    /// 评估条件组（支持 AND/OR 关系）
    /// </summary>
    private async Task<bool> EvaluateConditionGroupAsync(
        TenantId tenantId,
        long instanceId,
        JsonElement groupElement,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        if (!groupElement.TryGetProperty("conditions", out var conditionsProp) || conditionsProp.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var relationship = groupElement.TryGetProperty("relationship", out var relProp)
            ? relProp.GetString()?.ToUpperInvariant() ?? "AND"
            : "AND";

        var results = new List<bool>();
        foreach (var condition in conditionsProp.EnumerateArray())
        {
            // Support nested condition groups: if an element has "relationship", recurse into it
            var result = await EvaluateNestedConditionAsync(tenantId, instanceId, condition, instanceDataJson, cancellationToken);
            results.Add(result);
        }

        // 根据关系类型计算最终结果
        return relationship == "OR"
            ? results.Any(r => r) // OR: 任一条件满足即可
            : results.All(r => r); // AND: 所有条件都要满足
    }

    /// <summary>
    /// 评估单个条件
    /// </summary>
    private async Task<bool> EvaluateSingleConditionAsync(
        TenantId tenantId,
        long instanceId,
        JsonElement conditionElement,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        if (!conditionElement.TryGetProperty("field", out var fieldProp) ||
            !conditionElement.TryGetProperty("operator", out var operatorProp) ||
            !conditionElement.TryGetProperty("value", out var valueProp))
        {
            return false;
        }

        var fieldName = fieldProp.GetString();
        var operatorStr = operatorProp.GetString();
        // Bug fix: GetString() returns null for non-string JSON values (numbers, booleans).
        // Use GetRawText() as fallback to correctly handle numeric/boolean condition values.
        var expectedValue = valueProp.ValueKind switch
        {
            JsonValueKind.String => valueProp.GetString(),
            JsonValueKind.Null => null,
            _ => valueProp.GetRawText()
        };

        if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(operatorStr))
        {
            return false;
        }

        // 获取字段值（优先从流程变量，其次从实例数据JSON）
        var actualValue = await GetFieldValueAsync(tenantId, instanceId, fieldName, instanceDataJson, cancellationToken);

        // 执行比较
        return EvaluateOperator(actualValue, operatorStr, expectedValue);
    }

    /// <summary>
    /// 获取字段值（从流程变量或实例数据）
    /// </summary>
    private async Task<string?> GetFieldValueAsync(
        TenantId tenantId,
        long instanceId,
        string fieldName,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        // 优先从流程变量获取
        var variable = await _variableRepository.GetByInstanceAndNameAsync(tenantId, instanceId, fieldName, cancellationToken);
        if (variable != null && !string.IsNullOrEmpty(variable.VariableValue))
        {
            return variable.VariableValue;
        }

        // 其次从实例数据JSON获取
        if (!string.IsNullOrEmpty(instanceDataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(instanceDataJson);
                if (doc.RootElement.TryGetProperty(fieldName, out var prop))
                {
                    return prop.ValueKind switch
                    {
                        JsonValueKind.String => prop.GetString(),
                        JsonValueKind.Number => prop.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => null,
                        _ => prop.GetRawText()
                    };
                }
            }
            catch
            {
                // JSON解析失败，忽略
            }
        }

        return null;
    }

    /// <summary>
    /// 执行操作符比较
    /// </summary>
    private static bool EvaluateOperator(string? actualValue, string operatorStr, string? expectedValue)
    {
        // Normalize operator to lowercase once for correct matching.
        // Bug fix: previously ToLowerInvariant() was called but switch arms still used camelCase,
        // e.g. "notEquals" became "notequals" which didn't match the "notEquals" arm.
        var op = operatorStr.ToLowerInvariant();

        if (actualValue == null && expectedValue == null)
        {
            return op is "equals" or "==" or "eq" or "isempty" or "isnull";
        }

        if (actualValue == null || expectedValue == null)
        {
            return op is "notequals" or "!=" or "ne" or "isnotempty" or "isnotnull";
        }

        return op switch
        {
            "equals" or "==" or "eq" => actualValue == expectedValue,
            "notequals" or "!=" or "ne" => actualValue != expectedValue,
            "greaterthan" or ">" or "gt" => CompareNumbers(actualValue, expectedValue) > 0,
            "lessthan" or "<" or "lt" => CompareNumbers(actualValue, expectedValue) < 0,
            "greaterthanorequal" or ">=" or "ge" => CompareNumbers(actualValue, expectedValue) >= 0,
            "lessthanorequal" or "<=" or "le" => CompareNumbers(actualValue, expectedValue) <= 0,
            "contains" => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            "notcontains" => !actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            "startswith" => actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            "endswith" => actualValue.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            "in" => expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(v => v.Trim().Equals(actualValue, StringComparison.OrdinalIgnoreCase)),
            "notin" => !expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(v => v.Trim().Equals(actualValue, StringComparison.OrdinalIgnoreCase)),
            "isempty" => string.IsNullOrEmpty(actualValue),
            "isnotempty" => !string.IsNullOrEmpty(actualValue),
            "isnull" => actualValue == null,
            "isnotnull" => actualValue != null,
            _ => false
        };
    }

    /// <summary>
    /// 比较数字（支持整数和浮点数）
    /// </summary>
    private static int CompareNumbers(string a, string b)
    {
        // Bug fix: use InvariantCulture to avoid locale-dependent decimal separator issues.
        // Previously, "1,000" could parse as 1.0 in locales using comma as decimal separator.
        if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var numA)
            && double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out var numB))
        {
            return numA.CompareTo(numB);
        }

        // 无法解析为数字，按字符串比较
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 评估嵌套条件组（支持递归嵌套）
    /// </summary>
    private async Task<bool> EvaluateNestedConditionAsync(
        TenantId tenantId,
        long instanceId,
        JsonElement element,
        string? instanceDataJson,
        CancellationToken cancellationToken)
    {
        // If element has "relationship", it's a nested condition group
        if (element.TryGetProperty("relationship", out _))
        {
            return await EvaluateConditionGroupAsync(tenantId, instanceId, element, instanceDataJson, cancellationToken);
        }

        // Otherwise it's a single condition
        return await EvaluateSingleConditionAsync(tenantId, instanceId, element, instanceDataJson, cancellationToken);
    }
}
