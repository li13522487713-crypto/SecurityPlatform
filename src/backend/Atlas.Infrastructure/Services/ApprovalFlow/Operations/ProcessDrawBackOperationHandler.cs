using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 流程撤回操作处理器
/// </summary>
public sealed class ProcessDrawBackOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.ProcessDrawBack;

    public ProcessDrawBackOperationHandler(
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
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
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null)
        {
            throw new BusinessException("INSTANCE_NOT_FOUND", "流程实例不存在");
        }

        // 只有发起人可以撤回
        if (instance.InitiatorUserId != operatorUserId)
        {
            throw new BusinessException("UNAUTHORIZED", "只有发起人可以撤回流程");
        }

        // 只能撤回运行中的流程
        if (instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException("INSTANCE_NOT_RUNNING", "只能撤回运行中的流程");
        }

        // 检查是否已有审批
        var allTasks = (await _taskRepository.GetPagedByInstanceAsync(tenantId, instanceId, 1, 1000, cancellationToken: cancellationToken)).Items;
        var hasApproved = allTasks.Any(t => t.Status == ApprovalTaskStatus.Approved);

        if (hasApproved)
        {
            throw new BusinessException("CANNOT_DRAW_BACK", "流程已被审批，不允许撤回");
        }

        // 取消流程
        instance.MarkCanceled(DateTimeOffset.UtcNow);
        await _instanceRepository.UpdateAsync(instance, cancellationToken);

        // 取消所有待审批任务
        var pendingTasks = await _taskRepository.GetByInstanceAndStatusAsync(tenantId, instanceId, ApprovalTaskStatus.Pending, cancellationToken);
        foreach (var task in pendingTasks)
        {
            task.Cancel();
            await _taskRepository.UpdateAsync(task, cancellationToken);
        }

        // 记录撤回事件
        var drawBackEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.InstanceCanceled,
            null,
            null,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(drawBackEvent, cancellationToken);
    }
}





