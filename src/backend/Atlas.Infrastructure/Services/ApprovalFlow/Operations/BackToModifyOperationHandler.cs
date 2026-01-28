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
/// 打回修改操作处理器
/// </summary>
public sealed class BackToModifyOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGenerator _idGenerator;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.BackToModify;

    public BackToModifyOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGenerator idGenerator)
    {
        _instanceRepository = instanceRepository;
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
            throw new BusinessException("TASK_ID_REQUIRED", "打回修改操作需要指定任务ID");
        }

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new BusinessException("TASK_NOT_PENDING", "只能打回待审批的任务");
        }

        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null || instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "流程实例不在运行状态");
        }

        // 标记任务为驳回
        task.Reject(operatorUserId, request.Comment ?? "打回修改", DateTimeOffset.UtcNow);
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 取消所有待审批任务
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        foreach (var pendingTask in pendingTasks)
        {
            pendingTask.Cancel();
            await _taskRepository.UpdateAsync(pendingTask, cancellationToken);
        }

        // 流程状态改为驳回（打回修改）
        instance.MarkRejected(DateTimeOffset.UtcNow);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 记录打回事件
        var backToModifyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskRejected,
            task.NodeId,
            null,
            operatorUserId,
            _idGenerator.NextId());
        await _historyRepository.AddAsync(backToModifyEvent, cancellationToken);
    }
}
