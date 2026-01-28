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
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.BackToAnyNode;

    public BackToAnyNodeOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IIdGenerator idGenerator)
    {
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _flowRepository = flowRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _idGenerator = idGenerator;
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

        // 取消所有待审批任务
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        foreach (var pendingTask in pendingTasks)
        {
            pendingTask.Cancel();
            await _taskRepository.UpdateAsync(pendingTask, cancellationToken);
        }

        // 如果目标节点是审批节点，生成任务
        if (targetNode.Type == "approve")
        {
            // 生成任务
            var tasks = await ExpandTasksByAssigneeTypeAsync(
                tenantId,
                instanceId,
                targetNode.Id,
                targetNode.Label ?? "审批",
                targetNode.AssigneeType,
                targetNode.AssigneeValue ?? string.Empty,
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
                _idGenerator.NextId());
            await _nodeExecutionRepository.AddAsync(execution, cancellationToken);
        }

        // 更新实例当前节点
        instance.SetCurrentNode(request.TargetNodeId);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 记录退回事件
        var backToNodeEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            instance.CurrentNodeId,
            request.TargetNodeId,
            operatorUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(backToNodeEvent, cancellationToken);
    }

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
