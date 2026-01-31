using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 转发操作处理器（将任务转发给其他用户查看/处理，但不改变任务归属）
/// </summary>
public sealed class ForwardOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalTaskTransferRepository _transferRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Forward;

    public ForwardOperationHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalTaskTransferRepository transferRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _taskRepository = taskRepository;
        _transferRepository = transferRepository;
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
        if (!taskId.HasValue)
        {
            throw new BusinessException("TASK_ID_REQUIRED", "转发操作需要指定任务ID");
        }

        if (string.IsNullOrEmpty(request.TargetAssigneeValue))
        {
            throw new BusinessException("TARGET_ASSIGNEE_REQUIRED", "转发操作需要指定目标用户");
        }

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        // 验证操作人是否有权限转发（必须是当前处理人）
        if (task.AssigneeType != AssigneeType.User || task.AssigneeValue != operatorUserId.ToString())
        {
            throw new BusinessException("UNAUTHORIZED", "只能转发自己的任务");
        }

        // 转发操作：创建转发记录（转发不同于转办，转发后原任务仍然存在，只是通知目标用户）
        // 在当前实现中，转发可以创建一条转办记录用于审计，但不改变任务归属
        // 如果需要支持"转发后目标用户也能处理"，可以创建新的任务或使用转办逻辑

        // 记录转发（使用转办记录表记录转发关系）
        var forward = new ApprovalTaskTransfer(
            tenantId,
            taskId.Value,
            instanceId,
            task.NodeId,
            task.AssigneeValue,
            request.TargetAssigneeValue,
            operatorUserId,
            _idGeneratorAccessor.NextId(),
            request.Comment);
        await _transferRepository.AddAsync(forward, cancellationToken);

        // 记录历史事件
        var forwardEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            task.NodeId,
            task.NodeId,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(forwardEvent, cancellationToken);
    }
}





