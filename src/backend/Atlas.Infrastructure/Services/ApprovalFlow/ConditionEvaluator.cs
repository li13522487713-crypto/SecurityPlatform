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
            var result = await EvaluateSingleConditionAsync(tenantId, instanceId, condition, instanceDataJson, cancellationToken);
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
        var expectedValue = valueProp.GetString();

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
        if (actualValue == null && expectedValue == null)
        {
            return operatorStr == "equals" || operatorStr == "==";
        }

        if (actualValue == null || expectedValue == null)
        {
            return operatorStr == "notEquals" || operatorStr == "!=";
        }

        return operatorStr.ToLowerInvariant() switch
        {
            "equals" or "==" or "eq" => actualValue == expectedValue,
            "notEquals" or "!=" or "ne" => actualValue != expectedValue,
            "greaterThan" or ">" or "gt" => CompareNumbers(actualValue, expectedValue) > 0,
            "lessThan" or "<" or "lt" => CompareNumbers(actualValue, expectedValue) < 0,
            "greaterThanOrEqual" or ">=" or "ge" => CompareNumbers(actualValue, expectedValue) >= 0,
            "lessThanOrEqual" or "<=" or "le" => CompareNumbers(actualValue, expectedValue) <= 0,
            "contains" => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            "notContains" => !actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            "startsWith" => actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            "endsWith" => actualValue.EndsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            "in" => expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(v => v.Trim().Equals(actualValue, StringComparison.OrdinalIgnoreCase)),
            "notIn" => !expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(v => v.Trim().Equals(actualValue, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    /// <summary>
    /// 比较数字（支持整数和浮点数）
    /// </summary>
    private static int CompareNumbers(string a, string b)
    {
        // 尝试解析为数字
        if (double.TryParse(a, out var numA) && double.TryParse(b, out var numB))
        {
            return numA.CompareTo(numB);
        }

        // 无法解析为数字，按字符串比较
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
