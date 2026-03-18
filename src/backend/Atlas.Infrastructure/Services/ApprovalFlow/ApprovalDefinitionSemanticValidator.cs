using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

public sealed class ApprovalDefinitionSemanticValidator : IApprovalDefinitionSemanticValidator
{
    public IReadOnlyList<ApprovalFlowValidationIssue> Validate(string definitionJson)
    {
        var issues = new List<ApprovalFlowValidationIssue>();
        FlowDefinition definition;

        try
        {
            definition = FlowDefinitionParser.Parse(definitionJson);
        }
        catch (Exception ex)
        {
            issues.Add(CreateError("DEFINITION_PARSE_FAILED", $"流程定义解析失败：{ex.Message}"));
            return issues;
        }

        if (definition.Nodes.Count == 0)
        {
            issues.Add(CreateError("EMPTY_DEFINITION", "流程定义中没有可执行节点"));
            return issues;
        }

        ValidateEdges(definition, issues);
        ValidateReachability(definition, issues);

        foreach (var node in definition.Nodes)
        {
            switch (node.Type)
            {
                case "approve":
                    ValidateApproveNode(node, issues);
                    break;
                case "exclusiveGateway":
                case "inclusiveGateway":
                    ValidateConditionGateway(node, definition, issues);
                    break;
                case "parallelGateway":
                    ValidateParallelGateway(node, definition, issues);
                    break;
                case "routeGateway":
                    ValidateRouteGateway(node, definition, issues);
                    break;
                case "callProcess":
                    ValidateCallProcessNode(node, issues);
                    break;
                case "timer":
                    ValidateTimerNode(node, issues);
                    break;
                case "trigger":
                    ValidateTriggerNode(node, issues);
                    break;
            }
        }

        return issues;
    }

    private static void ValidateEdges(FlowDefinition definition, ICollection<ApprovalFlowValidationIssue> issues)
    {
        foreach (var edge in definition.Edges)
        {
            var edgeId = $"{edge.Source}->{edge.Target}";
            if (edge.Source == edge.Target)
            {
                issues.Add(CreateError("EDGE_SELF_LOOP", "连线不能指向自身节点", edgeId: edgeId));
            }
        }
    }

    private static void ValidateReachability(FlowDefinition definition, ICollection<ApprovalFlowValidationIssue> issues)
    {
        var startNode = definition.GetStartNode();
        if (startNode == null)
        {
            return;
        }

        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(startNode.Id);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reachable.Add(current))
            {
                continue;
            }

            foreach (var edge in definition.GetOutgoingEdges(current))
            {
                queue.Enqueue(edge.Target);
            }
        }

        foreach (var node in definition.Nodes.Where(node => !reachable.Contains(node.Id)))
        {
            issues.Add(CreateWarning("UNREACHABLE_NODE", "该节点无法从开始节点到达", nodeId: node.Id));
        }
    }

    private static void ValidateApproveNode(FlowNode node, ICollection<ApprovalFlowValidationIssue> issues)
    {
        if (RequireAssigneeValue(node.AssigneeType) && string.IsNullOrWhiteSpace(node.AssigneeValue))
        {
            issues.Add(CreateError("APPROVER_CONFIG_REQUIRED", "当前审批人类型缺少必要的审批人配置", node.Id));
        }

        if (node.AssigneeType == AssigneeType.Level &&
            (!int.TryParse(node.AssigneeValue, out var level) || level <= 0))
        {
            issues.Add(CreateError("LEVEL_ASSIGNEE_INVALID", "指定层级审批必须填写大于 0 的层级值", node.Id));
        }

        if (node.ApprovalMode != ApprovalMode.Vote)
        {
            return;
        }

        if (node.Weight is <= 0)
        {
            issues.Add(CreateError("VOTE_WEIGHT_INVALID", "票签模式下 voteWeight 必须大于 0", node.Id));
        }

        if (node.VotePassRate is < 1 or > 100)
        {
            issues.Add(CreateError("VOTE_PASS_RATE_INVALID", "票签模式下 votePassRate 必须在 1~100 之间", node.Id));
        }
    }

    private static void ValidateConditionGateway(
        FlowNode node,
        FlowDefinition definition,
        ICollection<ApprovalFlowValidationIssue> issues)
    {
        var outgoing = definition.GetOutgoingEdges(node.Id).Count;
        if (outgoing < 2)
        {
            issues.Add(CreateError("CONDITION_BRANCH_INCOMPLETE", "条件网关至少需要 2 条分支连线", node.Id));
        }
    }

    private static void ValidateParallelGateway(
        FlowNode node,
        FlowDefinition definition,
        ICollection<ApprovalFlowValidationIssue> issues)
    {
        var outgoingCount = definition.GetOutgoingEdges(node.Id).Count;
        var incomingCount = definition.GetIncomingEdges(node.Id).Count;

        // 并行分支节点
        if (outgoingCount > 1)
        {
            if (!HasReachableParallelJoin(definition, node.Id))
            {
                issues.Add(CreateError("PARALLEL_JOIN_MISSING", "并行分支缺少可达的汇聚节点", node.Id));
            }
        }

        // 并行汇聚节点
        if (incomingCount > 1 && outgoingCount != 1)
        {
            issues.Add(CreateError("PARALLEL_JOIN_INVALID", "并行汇聚节点必须且仅能有 1 条出边", node.Id));
        }
    }

    private static void ValidateRouteGateway(
        FlowNode node,
        FlowDefinition definition,
        ICollection<ApprovalFlowValidationIssue> issues)
    {
        var outgoingCount = definition.GetOutgoingEdges(node.Id).Count;
        if (outgoingCount != 1)
        {
            issues.Add(CreateError("ROUTE_TARGET_INVALID", "路由节点必须配置且仅配置 1 个目标节点", node.Id));
        }
    }

    private static bool HasReachableParallelJoin(FlowDefinition definition, string splitGatewayId)
    {
        var outgoingEdges = definition.GetOutgoingEdges(splitGatewayId);
        if (outgoingEdges.Count < 2)
        {
            return false;
        }

        HashSet<string>? commonJoinSet = null;
        foreach (var edge in outgoingEdges)
        {
            var branchJoins = FindReachableParallelJoinNodes(definition, edge.Target, splitGatewayId);
            if (branchJoins.Count == 0)
            {
                return false;
            }

            if (commonJoinSet == null)
            {
                commonJoinSet = branchJoins;
                continue;
            }

            commonJoinSet.IntersectWith(branchJoins);
            if (commonJoinSet.Count == 0)
            {
                return false;
            }
        }

        return commonJoinSet is { Count: > 0 };
    }

    private static HashSet<string> FindReachableParallelJoinNodes(
        FlowDefinition definition,
        string startNodeId,
        string splitGatewayId)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var joinIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue(startNodeId);

        while (queue.Count > 0)
        {
            var currentNodeId = queue.Dequeue();
            if (!visited.Add(currentNodeId))
            {
                continue;
            }

            var currentNode = definition.GetNodeById(currentNodeId);
            if (currentNode == null)
            {
                continue;
            }

            if (currentNode.Type == "parallelGateway" &&
                currentNode.Id != splitGatewayId &&
                definition.GetIncomingEdges(currentNodeId).Count > 1)
            {
                joinIds.Add(currentNodeId);
                continue;
            }

            foreach (var edge in definition.GetOutgoingEdges(currentNodeId))
            {
                queue.Enqueue(edge.Target);
            }
        }

        return joinIds;
    }

    private static bool RequireAssigneeValue(AssigneeType assigneeType)
    {
        return assigneeType is AssigneeType.User
            or AssigneeType.Role
            or AssigneeType.Level
            or AssigneeType.BusinessTable
            or AssigneeType.OutSideAccess;
    }

    private static ApprovalFlowValidationIssue CreateError(
        string code,
        string message,
        string? nodeId = null,
        string? edgeId = null)
    {
        return new ApprovalFlowValidationIssue(code, message, "error", nodeId, edgeId);
    }

    private static void ValidateCallProcessNode(
        FlowNode node,
        ICollection<ApprovalFlowValidationIssue> issues)
    {
        if (!node.CallProcessId.HasValue || node.CallProcessId.Value == 0)
        {
            issues.Add(CreateWarning("CALL_PROCESS_ID_MISSING", "子流程节点未配置目标流程 ID", node.Id));
        }
    }

    private static void ValidateTimerNode(
        FlowNode node,
        ICollection<ApprovalFlowValidationIssue> issues)
    {
        issues.Add(CreateWarning("TIMER_NODE_EXPERIMENTAL", "定时器节点为实验性功能，请确认运行时支持", node.Id));
    }

    private static void ValidateTriggerNode(
        FlowNode node,
        ICollection<ApprovalFlowValidationIssue> issues)
    {
        issues.Add(CreateWarning("TRIGGER_NODE_EXPERIMENTAL", "触发器节点为实验性功能，请确认运行时支持", node.Id));
    }

    private static ApprovalFlowValidationIssue CreateWarning(
        string code,
        string message,
        string? nodeId = null,
        string? edgeId = null)
    {
        return new ApprovalFlowValidationIssue(code, message, "warning", nodeId, edgeId);
    }
}
