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
/// 加批操作处理器（在当前节点后添加一个新的审批节点）
/// </summary>
public sealed class AddApprovalOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.AddApproval;

    public AddApprovalOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGenerator idGenerator)
    {
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _taskRepository = taskRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _historyRepository = historyRepository;
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
        if (string.IsNullOrEmpty(request.TargetAssigneeValue))
        {
            throw new BusinessException("TARGET_ASSIGNEE_REQUIRED", "加批操作需要指定审批人");
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

        // 加批操作：在当前节点后动态添加一个新的审批节点
        // 在当前实现中，我们创建一个临时的审批节点ID
        var addApprovalNodeId = $"add_approval_{_idGenerator.NextId()}";
        var currentNodeId = instance.CurrentNodeId ?? "start";

        // 创建加批节点（简化实现：直接在当前节点后生成任务）
        // 实际应用中，加批可能需要修改流程定义或创建动态节点
        // 这里简化实现：直接为指定审批人创建任务，节点ID使用特殊前缀标识
        
        var addApprovalTask = new ApprovalTask(
            tenantId,
            instanceId,
            addApprovalNodeId,
            $"加批审批 - {request.Comment ?? ""}",
            AssigneeType.User,
            request.TargetAssigneeValue,
            _idGenerator.NextId());
        await _taskRepository.AddAsync(addApprovalTask, cancellationToken);

        // 创建节点执行记录
        var execution = new ApprovalNodeExecution(
            tenantId,
            instanceId,
            addApprovalNodeId,
            ApprovalNodeExecutionStatus.Running,
            _idGenerator.NextId());
        await _nodeExecutionRepository.AddAsync(execution, cancellationToken);

        // 记录历史事件
        var addApprovalEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskCreated,
            currentNodeId,
            addApprovalNodeId,
            operatorUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(addApprovalEvent, cancellationToken);
    }
}
