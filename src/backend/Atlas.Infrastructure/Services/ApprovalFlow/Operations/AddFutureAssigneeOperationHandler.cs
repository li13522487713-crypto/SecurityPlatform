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
/// 未来节点加签操作处理器（为流程中尚未到达的节点添加审批人）
/// </summary>
public sealed class AddFutureAssigneeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalTaskAssigneeChangeRepository _assigneeChangeRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.AddFutureAssignee;

    public AddFutureAssigneeOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalTaskAssigneeChangeRepository assigneeChangeRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _taskRepository = taskRepository;
        _assigneeChangeRepository = assigneeChangeRepository;
        _historyRepository = historyRepository;
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
            throw new BusinessException("TARGET_NODE_REQUIRED", "未来节点加签操作需要指定目标节点ID");
        }

        if (request.AdditionalAssigneeValues == null || request.AdditionalAssigneeValues.Count == 0)
        {
            throw new BusinessException("ASSIGNEE_REQUIRED", "未来节点加签操作需要指定至少一个审批人");
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

        // 检查目标节点是否已经执行过
        var existingTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, request.TargetNodeId, cancellationToken);
        if (existingTasks.Count > 0)
        {
            throw new BusinessException("NODE_ALREADY_EXECUTED", "目标节点已经执行，无法进行未来节点加签");
        }

        // 为每个新审批人记录未来节点加签
        var changes = request.AdditionalAssigneeValues
            .Select(assigneeValue => new ApprovalTaskAssigneeChange(
                tenantId,
                instanceId,
                request.TargetNodeId,
                assigneeValue,
                AssigneeChangeType.AddFuture,
                operatorUserId,
                _idGeneratorAccessor.NextId(),
                null,
                request.Comment))
            .ToList();
        if (changes.Count > 0)
        {
            await _assigneeChangeRepository.AddRangeAsync(changes, cancellationToken);
        }

        // 记录历史事件
        var addFutureEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            instance.CurrentNodeId,
            request.TargetNodeId,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(addFutureEvent, cancellationToken);
    }
}


