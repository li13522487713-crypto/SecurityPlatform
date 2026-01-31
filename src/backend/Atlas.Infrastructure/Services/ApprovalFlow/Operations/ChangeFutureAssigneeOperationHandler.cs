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
/// 变更未来节点处理人操作处理器（变更流程中尚未到达的节点的处理人）
/// </summary>
public sealed class ChangeFutureAssigneeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalTaskAssigneeChangeRepository _assigneeChangeRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.ChangeFutureAssignee;

    public ChangeFutureAssigneeOperationHandler(
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
            throw new BusinessException("TARGET_NODE_REQUIRED", "变更未来节点处理人操作需要指定目标节点ID");
        }

        if (string.IsNullOrEmpty(request.TargetAssigneeValue))
        {
            throw new BusinessException("TARGET_ASSIGNEE_REQUIRED", "变更未来节点处理人操作需要指定目标处理人");
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

        // 检查目标节点是否已经执行过（如果已执行，则不是"未来节点"）
        var existingTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, request.TargetNodeId, cancellationToken);
        if (existingTasks.Count > 0)
        {
            throw new BusinessException("NODE_ALREADY_EXECUTED", "目标节点已经执行，无法变更未来节点处理人");
        }

        // 记录未来节点变更（当流程到达该节点时，会使用新的处理人）
        // 在当前实现中，我们可以在节点配置中存储变更信息，或者在生成任务时检查变更记录
        var change = new ApprovalTaskAssigneeChange(
            tenantId,
            instanceId,
            request.TargetNodeId,
            request.TargetAssigneeValue,
            AssigneeChangeType.Change,
            operatorUserId,
            _idGeneratorAccessor.NextId(),
            null,
            request.Comment);
        await _assigneeChangeRepository.AddAsync(change, cancellationToken);

        // 记录历史事件
        var changeEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            instance.CurrentNodeId,
            request.TargetNodeId,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(changeEvent, cancellationToken);
    }
}





