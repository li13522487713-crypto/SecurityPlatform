using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 转办操作处理器
/// </summary>
public sealed class TransferOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalTaskTransferRepository _transferRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Transfer;

    public TransferOperationHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalTaskTransferRepository transferRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGenerator idGenerator)
    {
        _taskRepository = taskRepository;
        _transferRepository = transferRepository;
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
        if (!taskId.HasValue)
        {
            throw new BusinessException("TASK_ID_REQUIRED", "转办操作需要指定任务ID");
        }

        if (string.IsNullOrEmpty(request.TargetAssigneeValue))
        {
            throw new BusinessException("TARGET_ASSIGNEE_REQUIRED", "转办操作需要指定目标处理人");
        }

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new BusinessException("TASK_NOT_PENDING", "只能转办待审批的任务");
        }

        // 验证操作人是否有权限转办（必须是当前处理人）
        if (task.AssigneeType != AssigneeType.User || task.AssigneeValue != operatorUserId.ToString())
        {
            throw new BusinessException("UNAUTHORIZED", "只能转办自己的任务");
        }

        // 记录转办
        var transfer = new ApprovalTaskTransfer(
            tenantId,
            taskId.Value,
            instanceId,
            task.NodeId,
            task.AssigneeValue,
            request.TargetAssigneeValue,
            operatorUserId,
            _idGenerator.NextId(),
            request.Comment);
        await _transferRepository.AddAsync(transfer, cancellationToken);

        // 更新任务处理人
        task.Transfer(request.TargetAssigneeValue);
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 记录历史事件
        var transferEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            task.NodeId,
            task.NodeId,
            operatorUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(transferEvent, cancellationToken);
    }
}
