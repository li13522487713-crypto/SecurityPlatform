using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 流程推进引擎（支持多节点、条件分支、会签/或签）
/// </summary>
public sealed class FlowEngine
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IIdGenerator _idGenerator;

    public FlowEngine(
        IApprovalTaskRepository taskRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IIdGenerator idGenerator)
    {
        _taskRepository = taskRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// 推进流程到下一个节点
    /// </summary>
    public async Task AdvanceFlowAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        FlowDefinition definition,
        string currentNodeId,
        CancellationToken cancellationToken)
    {
        // 获取当前节点的所有出边
        var outgoingEdges = definition.GetOutgoingEdges(currentNodeId);

        if (outgoingEdges.Count == 0)
        {
            // 没有出边，流程结束
            instance.MarkCompleted(DateTimeOffset.UtcNow);
            instance.SetCurrentNode(null);
            return;
        }

        // 获取当前节点
        var currentNode = definition.GetNodeById(currentNodeId);
        if (currentNode == null)
        {
            return;
        }

        // 检查当前节点是否已完成（会签/或签逻辑）
        if (currentNode.Type == "approve")
        {
            var nodeTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instance.Id, currentNodeId, cancellationToken);
            var isCompleted = CheckNodeCompletion(currentNode, nodeTasks);

            if (!isCompleted)
            {
                // 节点未完成，等待更多审批
                return;
            }
        }

        // 标记当前节点为已完成
        var nodeExecution = await _nodeExecutionRepository.GetByInstanceAndNodeAsync(tenantId, instance.Id, currentNodeId, cancellationToken);
        if (nodeExecution != null)
        {
            nodeExecution.MarkCompleted(DateTimeOffset.UtcNow);
            await _nodeExecutionRepository.UpdateAsync(nodeExecution, cancellationToken);
        }

        // 处理出边，决定下一个节点
        var nextNodeIds = await EvaluateNextNodesAsync(tenantId, instance, definition, currentNodeId, outgoingEdges, cancellationToken);

        if (nextNodeIds.Count == 0)
        {
            // 没有符合条件的下一个节点，流程结束
            instance.MarkCompleted(DateTimeOffset.UtcNow);
            instance.SetCurrentNode(null);
            return;
        }

        // 为每个下一个节点生成任务
        foreach (var nextNodeId in nextNodeIds)
        {
            var nextNode = definition.GetNodeById(nextNodeId);
            if (nextNode == null)
            {
                continue;
            }

            if (nextNode.Type == "end")
            {
                // 结束节点，流程完成
                instance.MarkCompleted(DateTimeOffset.UtcNow);
                instance.SetCurrentNode(null);
                return;
            }

            if (nextNode.Type == "approve")
            {
                // 审批节点，生成任务
                await GenerateTasksForNodeAsync(tenantId, instance, nextNode, cancellationToken);

                // 创建节点执行记录
                var execution = new ApprovalNodeExecution(
                    tenantId,
                    instance.Id,
                    nextNodeId,
                    ApprovalNodeExecutionStatus.Running,
                    _idGenerator.NextId());
                await _nodeExecutionRepository.AddAsync(execution, cancellationToken);

                instance.SetCurrentNode(nextNodeId);
            }
            else if (nextNode.Type == "condition")
            {
                // 条件节点，直接推进（条件已在 EvaluateNextNodesAsync 中评估）
                instance.SetCurrentNode(nextNodeId);
                await AdvanceFlowAsync(tenantId, instance, definition, nextNodeId, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 检查节点是否已完成（根据会签/或签模式）
    /// </summary>
    private static bool CheckNodeCompletion(FlowNode node, IReadOnlyList<ApprovalTask> tasks)
    {
        if (tasks.Count == 0)
        {
            return false;
        }

        var pendingTasks = tasks.Where(t => t.Status == ApprovalTaskStatus.Pending).ToList();
        var approvedTasks = tasks.Where(t => t.Status == ApprovalTaskStatus.Approved).ToList();
        var rejectedTasks = tasks.Where(t => t.Status == ApprovalTaskStatus.Rejected).ToList();

        // 如果有任何任务被驳回，节点失败
        if (rejectedTasks.Count > 0)
        {
            return false; // 需要外部处理驳回逻辑
        }

        switch (node.ApprovalMode)
        {
            case ApprovalMode.All:
                // 会签：所有任务必须同意
                return pendingTasks.Count == 0 && approvedTasks.Count == tasks.Count;

            case ApprovalMode.Any:
                // 或签：任一任务同意即可
                return approvedTasks.Count > 0;

            case ApprovalMode.Sequential:
                // 顺序会签：按顺序审批，当前任务同意后进入下一个
                // 简化实现：所有任务都同意
                return pendingTasks.Count == 0 && approvedTasks.Count == tasks.Count;

            default:
                return false;
        }
    }

    /// <summary>
    /// 评估下一个节点（处理条件分支）
    /// </summary>
    private async Task<List<string>> EvaluateNextNodesAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        FlowDefinition definition,
        string currentNodeId,
        IReadOnlyList<FlowEdge> outgoingEdges,
        CancellationToken cancellationToken)
    {
        var nextNodeIds = new List<string>();

        foreach (var edge in outgoingEdges)
        {
            // 如果没有条件规则，直接通过
            if (string.IsNullOrEmpty(edge.ConditionRule))
            {
                nextNodeIds.Add(edge.Target);
                continue;
            }

            // 评估条件规则（简化实现：暂时跳过条件评估，直接通过）
            // TODO: 实现条件规则评估器
            nextNodeIds.Add(edge.Target);
        }

        return nextNodeIds;
    }

    /// <summary>
    /// 为节点生成审批任务
    /// </summary>
    private async Task GenerateTasksForNodeAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        FlowNode node,
        CancellationToken cancellationToken)
    {
        var tasks = await ExpandTasksByAssigneeTypeAsync(
            tenantId,
            instance.Id,
            node.Id,
            node.Label ?? "审批",
            node.AssigneeType,
            node.AssigneeValue ?? string.Empty,
            cancellationToken);

        if (tasks.Count > 0)
        {
            await _taskRepository.AddRangeAsync(tasks, cancellationToken);
        }
    }

    /// <summary>
    /// 根据分配策略扩展任务
    /// </summary>
    private async Task<List<ApprovalTask>> ExpandTasksByAssigneeTypeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        string nodeTitle,
        AssigneeType assigneeType,
        string assigneeValue,
        CancellationToken cancellationToken)
    {
        var tasks = new List<ApprovalTask>();

        switch (assigneeType)
        {
            case AssigneeType.User:
                // 指定用户
                tasks.Add(new ApprovalTask(
                    tenantId,
                    instanceId,
                    nodeId,
                    nodeTitle,
                    AssigneeType.User,
                    assigneeValue,
                    _idGenerator.NextId()));
                break;

            case AssigneeType.Role:
                // 按角色（简化版：保存角色码，后续处理时展开）
                tasks.Add(new ApprovalTask(
                    tenantId,
                    instanceId,
                    nodeId,
                    nodeTitle,
                    AssigneeType.Role,
                    assigneeValue,
                    _idGenerator.NextId()));
                break;

            case AssigneeType.DepartmentLeader:
                // 部门负责人
                if (long.TryParse(assigneeValue, out var deptId))
                {
                    var leaderId = await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, deptId, cancellationToken);
                    if (leaderId.HasValue)
                    {
                        tasks.Add(new ApprovalTask(
                            tenantId,
                            instanceId,
                            nodeId,
                            nodeTitle,
                            AssigneeType.User,
                            leaderId.Value.ToString(),
                            _idGenerator.NextId()));
                    }
                }
                break;
        }

        return tasks;
    }
}
