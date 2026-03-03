using Atlas.Application.Approval.Models;
using FluentValidation;
using System.Text.Json;

namespace Atlas.Application.Approval.Validators;

/// <summary>
/// 更新审批流定义的验证器
/// </summary>
public sealed class ApprovalFlowDefinitionUpdateRequestValidator : AbstractValidator<ApprovalFlowDefinitionUpdateRequest>
{
    public ApprovalFlowDefinitionUpdateRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("流程ID必须大于0");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("流程名称不能为空")
            .MaximumLength(100).WithMessage("流程名称长度不超过100个字符");

        RuleFor(x => x.DefinitionJson)
            .NotEmpty().WithMessage("流程定义JSON不能为空")
            .Custom((json, ctx) => ValidateDefinitionJson(json, ctx));
    }

    private static void ValidateDefinitionJson(string json, ValidationContext<ApprovalFlowDefinitionUpdateRequest> ctx)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("nodes", out var nodesElement) &&
                nodesElement.ValueKind == JsonValueKind.Array)
            {
                ValidateGraphDefinition(nodesElement, root, ctx);
                return;
            }

            if (root.TryGetProperty("nodes", out var treeNodesElement) &&
                treeNodesElement.ValueKind == JsonValueKind.Object &&
                treeNodesElement.TryGetProperty("rootNode", out var rootNodeElement))
            {
                ValidateTreeDefinition(rootNodeElement, ctx);
                return;
            }

            ctx.AddFailure("DefinitionJson", "定义JSON结构不合法");
        }
        catch (JsonException ex)
        {
            ctx.AddFailure("DefinitionJson", $"JSON格式无效: {ex.Message}");
        }
    }

    private static void ValidateGraphDefinition(
        JsonElement nodesElement,
        JsonElement root,
        ValidationContext<ApprovalFlowDefinitionUpdateRequest> ctx)
    {
        if (!root.TryGetProperty("edges", out var edgesElement) ||
            edgesElement.ValueKind != JsonValueKind.Array)
        {
            ctx.AddFailure("DefinitionJson", "定义JSON必须包含'edges'数组");
            return;
        }

        var nodeIds = new HashSet<string>();
        var startNodeCount = 0;
        var endNodeCount = 0;

        foreach (var node in nodesElement.EnumerateArray())
        {
            if (!node.TryGetProperty("id", out var idProp))
            {
                ctx.AddFailure("DefinitionJson", "每个节点必须有'id'属性");
                return;
            }

            var nodeId = idProp.GetString();
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                ctx.AddFailure("DefinitionJson", "节点ID不能为空");
                return;
            }

            if (!nodeIds.Add(nodeId))
            {
                ctx.AddFailure("DefinitionJson", $"节点ID'{nodeId}'重复");
                return;
            }

            if (!node.TryGetProperty("type", out var typeProp))
            {
                ctx.AddFailure("DefinitionJson", $"节点'{nodeId}'必须有'type'属性");
                return;
            }

            var nodeType = typeProp.GetString();
            if (nodeType == "start")
                startNodeCount++;
            else if (nodeType == "end")
                endNodeCount++;

            if (nodeType == "condition" && node.TryGetProperty("conditionRule", out var ruleElement))
            {
                ValidateConditionRule(ruleElement, ctx);
            }
        }

        if (startNodeCount != 1)
        {
            ctx.AddFailure("DefinitionJson", "流程必须有且仅有1个开始节点");
        }

        if (endNodeCount < 1)
        {
            ctx.AddFailure("DefinitionJson", "流程必须至少有1个结束节点");
        }

        foreach (var edge in edgesElement.EnumerateArray())
        {
            if (edge.TryGetProperty("source", out var sourceProp))
            {
                var sourceId = sourceProp.GetString();
                if (!string.IsNullOrEmpty(sourceId) && !nodeIds.Contains(sourceId))
                {
                    ctx.AddFailure("DefinitionJson", $"边引用的源节点'{sourceId}'不存在");
                    return;
                }
            }

            if (edge.TryGetProperty("target", out var targetProp))
            {
                var targetId = targetProp.GetString();
                if (!string.IsNullOrEmpty(targetId) && !nodeIds.Contains(targetId))
                {
                    ctx.AddFailure("DefinitionJson", $"边引用的目标节点'{targetId}'不存在");
                    return;
                }
            }
        }
    }

    private static void ValidateTreeDefinition(JsonElement rootNodeElement, ValidationContext<ApprovalFlowDefinitionUpdateRequest> ctx)
    {
        if (!rootNodeElement.TryGetProperty("nodeType", out var typeProp) ||
            typeProp.GetString() != "start")
        {
            ctx.AddFailure("DefinitionJson", "根节点必须为start类型");
            return;
        }

        var hasEnd = false;
        var hasApprove = false;

        void Traverse(JsonElement node)
        {
            if (!node.TryGetProperty("nodeType", out var nodeTypeProp))
            {
                ctx.AddFailure("DefinitionJson", "节点必须包含nodeType");
                return;
            }

            var nodeType = nodeTypeProp.GetString();
            if (nodeType == "approve")
            {
                hasApprove = true;
            }

            if (nodeType == "end")
            {
                hasEnd = true;
            }

            if (nodeType == "condition" || nodeType == "dynamicCondition" || nodeType == "parallelCondition")
            {
                if (node.TryGetProperty("conditionNodes", out var branches) &&
                    branches.ValueKind == JsonValueKind.Array)
                {
                    foreach (var branch in branches.EnumerateArray())
                    {
                        if (branch.TryGetProperty("childNode", out var branchChild) &&
                            branchChild.ValueKind == JsonValueKind.Object)
                        {
                            Traverse(branchChild);
                        }
                    }
                }
            }

            if (nodeType == "parallel")
            {
                if (node.TryGetProperty("parallelNodes", out var parallelNodes) &&
                    parallelNodes.ValueKind == JsonValueKind.Array)
                {
                    var branchCount = 0;
                    foreach (var child in parallelNodes.EnumerateArray())
                    {
                        branchCount++;
                        Traverse(child);
                    }
                    if (branchCount < 2)
                    {
                        ctx.AddFailure("DefinitionJson", "并行节点至少需要2个并行分支");
                    }
                }
                else
                {
                    ctx.AddFailure("DefinitionJson", "并行节点必须包含 parallelNodes 数组");
                }

                if (!node.TryGetProperty("childNode", out var mergeNode) || mergeNode.ValueKind != JsonValueKind.Object)
                {
                    ctx.AddFailure("DefinitionJson", "并行节点必须配置汇聚后的后续节点");
                }
            }

            if (node.TryGetProperty("childNode", out var childNode) &&
                childNode.ValueKind == JsonValueKind.Object)
            {
                Traverse(childNode);
            }
        }

        Traverse(rootNodeElement);

        if (!hasEnd)
        {
            ctx.AddFailure("DefinitionJson", "流程必须至少有1个结束节点");
        }

        if (!hasApprove)
        {
            ctx.AddFailure("DefinitionJson", "流程必须至少有1个审批节点");
        }
    }

    private static void ValidateConditionRule(JsonElement ruleElement, ValidationContext<ApprovalFlowDefinitionUpdateRequest> ctx)
    {
        const string allowedOperators = "equals,notEquals,greaterThan,lessThan,greaterThanOrEqual,lessThanOrEqual,in,contains,startsWith,endsWith";
        var allowedSet = allowedOperators.Split(',');

        if (ruleElement.TryGetProperty("operator", out var opProp))
        {
            var op = opProp.GetString();
            if (!string.IsNullOrEmpty(op) && !allowedSet.Contains(op))
            {
                ctx.AddFailure("DefinitionJson", $"条件规则运算符'{op}'不被允许，仅支持：{allowedOperators}");
            }
        }

        var ruleJson = ruleElement.GetRawText();
        var forbiddenPatterns = new[] { "javascript:", "eval(", "function(", "script>" };
        foreach (var pattern in forbiddenPatterns)
        {
            if (ruleJson.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                ctx.AddFailure("DefinitionJson", $"条件规则包含不允许的内容：{pattern}");
                return;
            }
        }
    }
}
