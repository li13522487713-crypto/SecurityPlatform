using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 撤销同意操作处理器
/// </summary>
public sealed class DrawBackAgreeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.DrawBackAgree;

    public DrawBackAgreeOperationHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGenerator idGenerator)
    {
        _taskRepository = taskRepository;
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
            throw new BusinessException("TASK_ID_REQUIRED", "撤销同意操作需要指定任务ID");
        }

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        if (task.Status != ApprovalTaskStatus.Approved)
        {
            throw new BusinessException("TASK_NOT_APPROVED", "只能撤销已同意的任务");
        }

        // 验证操作人是否有权限撤销（必须是原审批人）
        if (task.DecisionByUserId != operatorUserId)
        {
            throw new BusinessException("UNAUTHORIZED", "只能撤销自己的审批");
        }

        // 将任务状态改回待审批
        // 注意：这里需要扩展 ApprovalTask 实体以支持撤销同意
        // 简化实现：创建一个新的待审批任务
        var newTask = new ApprovalTask(
            tenantId,
            task.InstanceId,
            task.NodeId,
            task.Title,
            task.AssigneeType,
            task.AssigneeValue,
            _idGenerator.NextId());
        await _taskRepository.AddAsync(newTask, cancellationToken);

        // 取消原任务
        task.Cancel();
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 记录撤销事件
        var drawBackEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskCreated,
            null,
            task.NodeId,
            operatorUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(drawBackEvent, cancellationToken);
    }
}
