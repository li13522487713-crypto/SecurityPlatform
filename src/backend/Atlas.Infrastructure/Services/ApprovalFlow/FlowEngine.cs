using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;
using ParallelTokenStatus = Atlas.Domain.Approval.Entities.ParallelTokenStatus;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 流程推进引擎（支持多节点、条件分支、会签/或签、并行网关、抄送节点）
/// </summary>
public sealed class FlowEngine
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IApprovalParallelTokenRepository _parallelTokenRepository;
    private readonly IApprovalCopyRecordRepository _copyRecordRepository;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IIdGenerator _idGenerator;

    public FlowEngine(
        IApprovalTaskRepository taskRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IApprovalParallelTokenRepository parallelTokenRepository,
        IApprovalCopyRecordRepository copyRecordRepository,
        ConditionEvaluator conditionEvaluator,
        IIdGenerator idGenerator)
    {
        _taskRepository = taskRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _parallelTokenRepository = parallelTokenRepository;
        _copyRecordRepository = copyRecordRepository;
        _conditionEvaluator = conditionEvaluator;
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
        // 获取当前节点
        var currentNode = definition.GetNodeById(currentNodeId);
        if (currentNode == null)
        {
            return;
        }

        // 检查并行汇聚网关：如果是并行汇聚网关，需要等待所有分支完成
        if (definition.IsParallelJoinGateway(currentNodeId))
        {
            var canProceed = await CheckParallelJoinCompletionAsync(tenantId, instance.Id, currentNodeId, definition, cancellationToken);
            if (!canProceed)
            {
                // 等待所有分支完成
                return;
            }
        }

        // 检查当前节点是否已完成（会签/或签逻辑）
        if (currentNode.Type == "approve")
        {
            var nodeTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instance.Id, currentNodeId, cancellationToken);
            
            // 顺序会签：检查是否需要激活下一个任务
            if (currentNode.ApprovalMode == ApprovalMode.Sequential)
            {
                await ActivateNextSequentialTaskAsync(tenantId, instance.Id, currentNodeId, nodeTasks, cancellationToken);
            }

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

        // 处理并行汇聚：标记当前分支的token为已完成
        if (definition.IsParallelJoinGateway(currentNodeId))
        {
            await MarkParallelBranchCompletedAsync(tenantId, instance.Id, currentNodeId, definition, cancellationToken);
        }

        // 获取当前节点的所有出边
        var outgoingEdges = definition.GetOutgoingEdges(currentNodeId);

        if (outgoingEdges.Count == 0)
        {
            // 没有出边，流程结束
            instance.MarkCompleted(DateTimeOffset.UtcNow);
            instance.SetCurrentNode(null);
            return;
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

        // 处理并行分支网关：创建token并推进所有分支
        if (definition.IsParallelSplitGateway(currentNodeId))
        {
            await HandleParallelSplitAsync(tenantId, instance, definition, currentNodeId, nextNodeIds, cancellationToken);
            return;
        }

        // 为每个下一个节点生成任务
        foreach (var nextNodeId in nextNodeIds)
        {
            await ProcessNextNodeAsync(tenantId, instance, definition, nextNodeId, cancellationToken);
        }
    }

    /// <summary>
    /// 处理下一个节点
    /// </summary>
    private async Task ProcessNextNodeAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        FlowDefinition definition,
        string nextNodeId,
        CancellationToken cancellationToken)
    {
        var nextNode = definition.GetNodeById(nextNodeId);
        if (nextNode == null)
        {
            return;
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
        else if (nextNode.Type == "condition" || nextNode.Type == "externalCondition")
        {
            // 条件节点（内部或外部），直接推进（条件已在 EvaluateNextNodesAsync 中评估）
            var execution = new ApprovalNodeExecution(
                tenantId,
                instance.Id,
                nextNodeId,
                ApprovalNodeExecutionStatus.Running,
                _idGenerator.NextId());
            await _nodeExecutionRepository.AddAsync(execution, cancellationToken);
            instance.SetCurrentNode(nextNodeId);
            await AdvanceFlowAsync(tenantId, instance, definition, nextNodeId, cancellationToken);
        }
        else if (nextNode.Type == "copy")
        {
            // 抄送节点：生成抄送记录（不阻塞流程）
            await GenerateCopyRecordsForNodeAsync(tenantId, instance, nextNode, cancellationToken);

            // 创建节点执行记录并标记为已完成
            var execution = new ApprovalNodeExecution(
                tenantId,
                instance.Id,
                nextNodeId,
                ApprovalNodeExecutionStatus.Completed,
                _idGenerator.NextId());
            await _nodeExecutionRepository.AddAsync(execution, cancellationToken);

            // 抄送节点不阻塞流程，继续推进
            var outgoingEdges = definition.GetOutgoingEdges(nextNodeId);
            if (outgoingEdges.Count > 0)
            {
                var nextAfterCopy = await EvaluateNextNodesAsync(tenantId, instance, definition, nextNodeId, outgoingEdges, cancellationToken);
                foreach (var nodeId in nextAfterCopy)
                {
                    await ProcessNextNodeAsync(tenantId, instance, definition, nodeId, cancellationToken);
                }
            }
        }
        else if (nextNode.Type == "exclusiveGateway" || nextNode.Type == "parallelGateway")
        {
            // 网关节点：直接推进
            var execution = new ApprovalNodeExecution(
                tenantId,
                instance.Id,
                nextNodeId,
                ApprovalNodeExecutionStatus.Running,
                _idGenerator.NextId());
            await _nodeExecutionRepository.AddAsync(execution, cancellationToken);
            instance.SetCurrentNode(nextNodeId);
            await AdvanceFlowAsync(tenantId, instance, definition, nextNodeId, cancellationToken);
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
                // 顺序会签：按顺序审批，当前激活的任务完成即可
                // 检查是否有等待中的任务（说明还有未完成的任务）
                var waitingTasks = tasks.Where(t => t.Status == ApprovalTaskStatus.Waiting).ToList();
                // 如果没有等待任务且所有已激活的任务都已完成，则节点完成
                return waitingTasks.Count == 0 && pendingTasks.Count == 0 && approvedTasks.Count > 0;

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
        var currentNode = definition.GetNodeById(currentNodeId);

        // 判断是否为排他网关（XOR）：只走一条符合条件的路径
        var isExclusiveGateway = currentNode != null && currentNode.Type == "exclusiveGateway";

        foreach (var edge in outgoingEdges)
        {
            // 如果没有条件规则，直接通过
            if (string.IsNullOrEmpty(edge.ConditionRule))
            {
                if (isExclusiveGateway)
                {
                    // 排他网关：无条件路径优先，找到第一个就返回
                    return new List<string> { edge.Target };
                }
                nextNodeIds.Add(edge.Target);
                continue;
            }

            // 评估条件规则
            var passed = await _conditionEvaluator.EvaluateAsync(
                tenantId,
                instance.Id,
                edge.ConditionRule,
                instance.DataJson,
                cancellationToken);

            if (passed)
            {
                if (isExclusiveGateway)
                {
                    // 排他网关：找到第一个符合条件的路径就返回
                    return new List<string> { edge.Target };
                }
                nextNodeIds.Add(edge.Target);
            }
        }

        // 排他网关如果没有符合条件的路径，返回空列表（流程可能卡住或结束）
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
            node.ApprovalMode,
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
        ApprovalMode approvalMode,
        CancellationToken cancellationToken)
    {
        var tasks = new List<ApprovalTask>();
        var userIds = new List<string>();

        switch (assigneeType)
        {
            case AssigneeType.User:
                // 指定用户（可能是多个用户，逗号分隔或JSON数组）
                userIds = ParseUserIds(assigneeValue);
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
                    _idGenerator.NextId(),
                    order: 0,
                    initialStatus: ApprovalTaskStatus.Pending));
                break;

            case AssigneeType.DepartmentLeader:
                // 部门负责人
                if (long.TryParse(assigneeValue, out var deptId))
                {
                    var leaderId = await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, deptId, cancellationToken);
                    if (leaderId.HasValue)
                    {
                        userIds.Add(leaderId.Value.ToString());
                    }
                }
                break;
        }

        // 为每个用户创建任务
        int order = 1;
        foreach (var userId in userIds)
        {
            // 顺序会签：第一个任务为Pending，其他为Waiting
            var initialStatus = approvalMode == ApprovalMode.Sequential && order > 1
                ? ApprovalTaskStatus.Waiting
                : ApprovalTaskStatus.Pending;

            var task = new ApprovalTask(
                tenantId,
                instanceId,
                nodeId,
                nodeTitle,
                AssigneeType.User,
                userId,
                _idGenerator.NextId(),
                order: order,
                initialStatus: initialStatus);

            tasks.Add(task);
            order++;
        }

        return tasks;
    }

    /// <summary>
    /// 解析用户ID列表（支持逗号分隔或JSON数组）
    /// </summary>
    private static List<string> ParseUserIds(string assigneeValue)
    {
        var userIds = new List<string>();
        if (string.IsNullOrEmpty(assigneeValue))
        {
            return userIds;
        }

        try
        {
            // 尝试解析为JSON数组
            using var doc = System.Text.Json.JsonDocument.Parse(assigneeValue);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var userId = element.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.Number => element.GetInt64().ToString(),
                        System.Text.Json.JsonValueKind.String => element.GetString(),
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(userId))
                    {
                        userIds.Add(userId);
                    }
                }
                return userIds;
            }
        }
        catch
        {
            // 不是JSON，继续尝试逗号分隔
        }

        // 逗号分隔
        var parts = assigneeValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                userIds.Add(trimmed);
            }
        }

        return userIds;
    }

    /// <summary>
    /// 检查并行汇聚网关是否所有分支都已完成
    /// </summary>
    private async Task<bool> CheckParallelJoinCompletionAsync(
        TenantId tenantId,
        long instanceId,
        string gatewayNodeId,
        FlowDefinition definition,
        CancellationToken cancellationToken)
    {
        // 获取所有指向该汇聚网关的入边
        var incomingEdges = definition.GetIncomingEdges(gatewayNodeId);
        if (incomingEdges.Count <= 1)
        {
            return true; // 不是并行汇聚
        }

        // 获取该网关的所有token
        var tokens = await _parallelTokenRepository.GetByInstanceAndGatewayAsync(tenantId, instanceId, gatewayNodeId, cancellationToken);

        // 检查每个分支是否都有已完成的token
        var completedBranches = tokens.Where(t => t.Status == ParallelTokenStatus.Completed).Select(t => t.BranchNodeId).ToHashSet();
        var requiredBranches = incomingEdges.Select(e => e.Source).ToHashSet();

        return requiredBranches.All(branch => completedBranches.Contains(branch));
    }

    /// <summary>
    /// 标记并行分支为已完成
    /// </summary>
    private async Task MarkParallelBranchCompletedAsync(
        TenantId tenantId,
        long instanceId,
        string gatewayNodeId,
        FlowDefinition definition,
        CancellationToken cancellationToken)
    {
        // 找到当前到达汇聚网关的分支（通过入边找到来源节点）
        var incomingEdges = definition.GetIncomingEdges(gatewayNodeId);
        foreach (var edge in incomingEdges)
        {
            // 检查该分支的token是否存在且未完成
            var tokens = await _parallelTokenRepository.GetByInstanceAndGatewayAsync(tenantId, instanceId, gatewayNodeId, cancellationToken);
            var branchToken = tokens.FirstOrDefault(t => t.BranchNodeId == edge.Source && t.Status == ParallelTokenStatus.Active);

            if (branchToken != null)
            {
                branchToken.MarkCompleted(DateTimeOffset.UtcNow);
                await _parallelTokenRepository.UpdateAsync(branchToken, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 处理并行分支网关：创建token并推进所有分支
    /// </summary>
    private async Task HandleParallelSplitAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        FlowDefinition definition,
        string gatewayNodeId,
        IReadOnlyList<string> nextNodeIds,
        CancellationToken cancellationToken)
    {
        // 为每个分支创建token
        foreach (var nextNodeId in nextNodeIds)
        {
            var token = new ApprovalParallelToken(
                tenantId,
                instance.Id,
                gatewayNodeId,
                nextNodeId,
                _idGenerator.NextId());
            await _parallelTokenRepository.AddAsync(token, cancellationToken);
        }

        // 推进所有分支
        foreach (var nextNodeId in nextNodeIds)
        {
            await ProcessNextNodeAsync(tenantId, instance, definition, nextNodeId, cancellationToken);
        }
    }

    /// <summary>
    /// 激活顺序会签的下一个任务
    /// </summary>
    private async Task ActivateNextSequentialTaskAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        IReadOnlyList<ApprovalTask> tasks,
        CancellationToken cancellationToken)
    {
        // 找到已完成的最高顺序号
        var completedMaxOrder = tasks
            .Where(t => t.Status == ApprovalTaskStatus.Approved)
            .Select(t => t.Order)
            .DefaultIfEmpty(0)
            .Max();

        // 找到下一个等待激活的任务
        var nextTask = tasks
            .Where(t => t.Status == ApprovalTaskStatus.Waiting && t.Order == completedMaxOrder + 1)
            .OrderBy(t => t.Order)
            .FirstOrDefault();

        if (nextTask != null)
        {
            nextTask.Activate();
            await _taskRepository.UpdateAsync(nextTask, cancellationToken);
        }
    }

    /// <summary>
    /// 为抄送节点生成抄送记录
    /// </summary>
    private async Task GenerateCopyRecordsForNodeAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        FlowNode node,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(node.AssigneeValue))
        {
            return;
        }

        // 解析收件人列表（支持JSON数组或逗号分隔的用户ID）
        var recipientIds = new List<long>();
        try
        {
            // 尝试解析为JSON数组
            using var doc = System.Text.Json.JsonDocument.Parse(node.AssigneeValue);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        recipientIds.Add(element.GetInt64());
                    }
                    else if (element.ValueKind == System.Text.Json.JsonValueKind.String && long.TryParse(element.GetString(), out var userId))
                    {
                        recipientIds.Add(userId);
                    }
                }
            }
        }
        catch
        {
            // 如果不是JSON，尝试逗号分隔
            var parts = node.AssigneeValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (long.TryParse(part.Trim(), out var userId))
                {
                    recipientIds.Add(userId);
                }
            }
        }

        // 创建抄送记录
        var copyRecords = recipientIds.Select(userId => new ApprovalCopyRecord(
            tenantId,
            instance.Id,
            node.Id,
            userId,
            _idGenerator.NextId())).ToList();

        if (copyRecords.Count > 0)
        {
            await _copyRecordRepository.AddRangeAsync(copyRecords, cancellationToken);
        }
    }
}
