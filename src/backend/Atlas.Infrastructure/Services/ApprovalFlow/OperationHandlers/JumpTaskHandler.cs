using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 跳转任务处理器
/// </summary>
public sealed class JumpTaskHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly FlowEngine _flowEngine;

    public JumpTaskHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalHistoryRepository historyRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        FlowEngine flowEngine)
    {
        _taskRepository = taskRepository;
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _historyRepository = historyRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _flowEngine = flowEngine;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Jump;

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
            throw new BusinessException("INVALID_REQUEST", "跳转目标节点不能为空");
        }

        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null || instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "流程实例不在运行状态");
        }

        // 取消当前所有活跃任务
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        var waitingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Waiting, cancellationToken);
        var allActiveTasks = pendingTasks.Concat(waitingTasks).ToList();

        foreach (var task in allActiveTasks)
        {
            task.Cancel();
        }
        await _taskRepository.UpdateRangeAsync(allActiveTasks, cancellationToken);

        // 记录跳转历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskJumped,
            request.TargetNodeId,
            null,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);

        // 加载流程定义
        var flowDef = await _flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
        if (flowDef == null) throw new BusinessException("FLOW_NOT_FOUND", "流程定义不存在");
        var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);

        // 推进到目标节点
        // 注意：FlowEngine.AdvanceFlowAsync 是从当前节点推进到下一个节点
        // 这里我们需要直接在目标节点生成任务，而不是从目标节点的前一个节点推进
        // 但 FlowEngine 没有公开直接在指定节点生成任务的方法（GenerateTasksForNodeAsync 是私有的）
        // 我们可以模拟从目标节点的入边推进，或者修改 FlowEngine 暴露方法
        // 或者，我们可以创建一个临时的“跳转”机制：
        // 1. 设置 CurrentNodeId 为目标节点的前一个节点（如果有的话）
        // 2. 调用 AdvanceFlowAsync
        // 但这样比较复杂，因为可能有多个前驱。
        
        // 更好的方式是：在 FlowEngine 中添加 JumpToNodeAsync 方法
        // 或者，我们这里手动调用 ProcessNextNodeAsync (如果是 public 的)
        // ProcessNextNodeAsync 是 private 的。
        
        // 既然我们在 Infrastructure 层，我们可以通过反射或者修改 FlowEngine。
        // 为了规范，我们应该在 FlowEngine 中添加 JumpToNodeAsync。
        // 但现在我不能修改 FlowEngine（那是 Phase 1 的任务，虽然我可以回去改，但最好保持计划顺序）
        // 不过，FlowEngine.AdvanceFlowAsync 的逻辑是：完成当前节点 -> 找下一个节点 -> ProcessNextNodeAsync
        // 如果我们想跳转到 TargetNode，我们可以直接调用 ProcessNextNodeAsync(..., TargetNodeId, ...)
        
        // 让我们假设 FlowEngine 有一个 JumpToNodeAsync 方法，或者我添加一个。
        // 鉴于我还在 Phase 2，我可以修改 Phase 1 的文件如果需要。
        // 我将修改 FlowEngine 添加 JumpToNodeAsync。
        
        await _flowEngine.JumpToNodeAsync(tenantId, instance, flowDefinition, request.TargetNodeId, cancellationToken);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);
    }
}
