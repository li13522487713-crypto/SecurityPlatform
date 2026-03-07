using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 退回任意节点操作处理器
/// </summary>
public sealed class BackToAnyNodeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly AssigneeResolver _assigneeResolver;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.BackToAnyNode;

    public BackToAnyNodeOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        AssigneeResolver assigneeResolver,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _flowRepository = flowRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _assigneeResolver = assigneeResolver;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.TargetNodeId))
        {
            throw new BusinessException("TARGET_NODE_REQUIRED", "退回任意节点操作需要指定目标节点ID");
        }

        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null || instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "流程实例不在运行状态");
        }

        ApprovalTask? task = null;
        if (taskId.HasValue)
        {
            task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
            if (task == null)
            {
                throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
            }

            if (task.InstanceId != instanceId)
            {
                throw new BusinessException("TASK_INSTANCE_MISMATCH", "任务不属于指定的流程实例");
            }

            if (task.Status != ApprovalTaskStatus.Pending)
            {
                throw new BusinessException("TASK_NOT_PENDING", "只能基于待审批任务执行退回任意节点");
            }
        }

        // 授权校验：仅流程发起人或当前待办处理人可执行退回任意节点
        var isInitiator = instance.InitiatorUserId == operatorUserId;
        var isCurrentAssignee = task is not null
            && task.AssigneeType == AssigneeType.User
            && task.AssigneeValue == operatorUserId.ToString();
        if (!isInitiator && !isCurrentAssignee)
        {
            throw new BusinessException("FORBIDDEN", "只有发起人或当前处理人可以执行退回操作");
        }

        var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
        if (flowDef == null)
        {
            throw new BusinessException("FLOW_NOT_FOUND", "流程定义不存在");
        }

        var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
        var targetNode = flowDefinition.GetNodeById(request.TargetNodeId);
        if (targetNode == null)
        {
            throw new BusinessException("NODE_NOT_FOUND", "目标节点不存在");
        }

        // 取消所有活跃任务（含 Pending 和 Waiting，修复顺签模式下遗漏 Waiting 任务的 bug）
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        var waitingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Waiting, cancellationToken);
        var allActiveTasks = new List<ApprovalTask>();
        allActiveTasks.AddRange(pendingTasks);
        allActiveTasks.AddRange(waitingTasks);
        if (allActiveTasks.Count > 0)
        {
            foreach (var activeTask in allActiveTasks)
            {
                activeTask.Cancel();
            }
            await _taskRepository.UpdateRangeAsync(allActiveTasks, cancellationToken);
        }

        // 如果目标节点是审批节点，生成任务
        if (targetNode.Type == "approve")
        {
            // 生成任务
            var tasks = await ExpandTasksByAssigneeTypeAsync(
                tenantId,
                instance,
                targetNode.Id,
                targetNode.Label ?? "审批",
                targetNode.AssigneeType,
                targetNode.AssigneeValue ?? string.Empty,
                targetNode.ApprovalMode,
                targetNode.MissingAssigneeStrategy,
                cancellationToken);

            if (tasks.Count > 0)
            {
                await _taskRepository.AddRangeAsync(tasks, cancellationToken);
            }

            // 创建节点执行记录
            var execution = new ApprovalNodeExecution(
                tenantId,
                instanceId,
                targetNode.Id,
                ApprovalNodeExecutionStatus.Running,
                _idGeneratorAccessor.NextId());
            await _nodeExecutionRepository.AddAsync(execution, cancellationToken);
        }

        // Bug fix: capture the current node BEFORE overwriting it, so the history event
        // correctly records the from-node and to-node (previously both were the target).
        var previousNodeId = instance.CurrentNodeId;

        // 更新实例当前节点
        instance.SetCurrentNode(request.TargetNodeId);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 记录退回事件（Bug fix: previously used NodeAdvanced, now uses dedicated BackToAnyNode type）
        var backToNodeEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.BackToAnyNode,
            previousNodeId,
            request.TargetNodeId,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(backToNodeEvent, cancellationToken);
    }

    private async Task<List<ApprovalTask>> ExpandTasksByAssigneeTypeAsync(
        TenantId tenantId,
        ApprovalProcessInstance instance,
        string nodeId,
        string nodeTitle,
        AssigneeType assigneeType,
        string assigneeValue,
        ApprovalMode approvalMode,
        MissingAssigneeStrategy missingAssigneeStrategy,
        CancellationToken cancellationToken)
    {
        var tasks = new List<ApprovalTask>();
        var userIds = await _assigneeResolver.ResolveUserIdsAsync(
            tenantId,
            instance.InitiatorUserId,
            assigneeType,
            assigneeValue,
            instance.DataJson,
            cancellationToken);

        // 处理缺失审批人策略
        if (userIds.Count == 0)
        {
            switch (missingAssigneeStrategy)
            {
                case MissingAssigneeStrategy.NotAllowed:
                    throw new BusinessException("MISSING_ASSIGNEE", $"节点 {nodeId} 无法找到审批人，不允许发起流程");

                case MissingAssigneeStrategy.Skip:
                    return tasks;

                case MissingAssigneeStrategy.TransferToAdmin:
                var adminUserIds = await _assigneeResolver.ResolveUserIdsAsync(
                    tenantId,
                    instance.InitiatorUserId,
                    AssigneeType.Role,
                    "Admin",
                    instance.DataJson,
                    cancellationToken);
                    if (adminUserIds.Count > 0)
                    {
                        userIds.AddRange(adminUserIds);
                    }
                    else
                    {
                        return tasks;
                    }
                    break;
            }
        }

        // 为每个用户创建任务
        int order = 1;
        foreach (var userId in userIds.Distinct())
        {
            var initialStatus = approvalMode == ApprovalMode.Sequential && order > 1
                ? ApprovalTaskStatus.Waiting
                : ApprovalTaskStatus.Pending;

            var task = new ApprovalTask(
                tenantId,
                instance.Id,
                nodeId,
                nodeTitle,
                AssigneeType.User,
                userId.ToString(),
                _idGeneratorAccessor.NextId(),
                order: order,
                initialStatus: initialStatus);

            tasks.Add(task);
            order++;
        }

        return tasks;
    }
}





