using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 释放任务处理器（取消认领，放回池子）
/// </summary>
public sealed class ReleaseTaskHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ReleaseTaskHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Release;

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (!taskId.HasValue) throw new BusinessException("INVALID_REQUEST", "任务ID不能为空");

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null) throw new BusinessException("TASK_NOT_FOUND", "任务不存在");

        // 必须是已认领的任务才能释放
        // 假设我们用某种方式标记了已认领（比如 Status=Claimed 或者 TaskType）
        
        // 恢复其他竞争任务
        // 这比较难，因为其他任务已经 Cancelled 了。
        // 需要重新生成竞争任务，或者把 Cancelled 的任务恢复为 Pending。
        
        var siblingTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, task.NodeId, cancellationToken);
        var cancelledTasks = siblingTasks.Where(t => t.Status == ApprovalTaskStatus.Canceled).ToList();
        
        foreach (var t in cancelledTasks)
        {
            t.Activate(); // 变回 Pending
        }
        await _taskRepository.UpdateRangeAsync(cancelledTasks, cancellationToken);

        // 记录释放历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskTransferred, // 暂时用 Transferred 或新增 Released
            task.NodeId,
            null,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }
}
