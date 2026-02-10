using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 离职转办处理器（批量）
/// </summary>
public sealed class BatchTransferHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public BatchTransferHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.BatchTransfer;

    public async Task ExecuteAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationRequest request,
        CancellationToken cancellationToken)
    {
        // 这里的 instanceId 可能不适用，因为是批量操作。
        // 但 IApprovalOperationHandler 接口设计是针对单个实例的。
        // 如果复用此接口，可能需要循环调用，或者 BatchTransferHandler 不实现此接口，而是单独的服务方法。
        // 鉴于 BatchTransfer 是管理功能，通常在 CommandService 中实现 BatchTransferTasksAsync。
        // 这里仅作为单个任务转办的实现（如果需要）。
        
        // 假设这里处理的是“将当前实例中某人的任务转给另一个人”
        if (string.IsNullOrEmpty(request.TargetAssigneeValue)) throw new BusinessException("INVALID_REQUEST", "转办目标不能为空");

        // 查找该实例中所有待办任务，且处理人是 operatorUserId (或者 request 指定的 SourceUser)
        // 这里简单处理：转办 taskId 指定的任务
        if (taskId.HasValue)
        {
            var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
            if (task != null)
            {
                task.Transfer(request.TargetAssigneeValue); // 假设 ApprovalTask 有 Transfer 方法修改 AssigneeValue
                await _taskRepository.UpdateAsync(task, cancellationToken);
                
                // 记录历史
                var historyEvent = new ApprovalHistoryEvent(
                    tenantId,
                    instanceId,
                    ApprovalHistoryEventType.TaskTransferred,
                    task.NodeId,
                    request.Comment,
                    operatorUserId,
                    _idGeneratorAccessor.NextId());
                await _historyRepository.AddAsync(historyEvent, cancellationToken);
            }
        }
    }
}
