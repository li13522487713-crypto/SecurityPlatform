using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 变更处理人操作处理器（变更当前任务的处理人）
/// </summary>
public sealed class ChangeAssigneeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalTaskAssigneeChangeRepository _assigneeChangeRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.ChangeAssignee;

    public ChangeAssigneeOperationHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalTaskAssigneeChangeRepository assigneeChangeRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
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
        if (!taskId.HasValue)
        {
            throw new BusinessException("TASK_ID_REQUIRED", "变更处理人操作需要指定任务ID");
        }

        if (string.IsNullOrEmpty(request.TargetAssigneeValue))
        {
            throw new BusinessException("TARGET_ASSIGNEE_REQUIRED", "变更处理人操作需要指定目标处理人");
        }

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new BusinessException("TASK_NOT_PENDING", "只能变更待审批任务的处理人");
        }

        // 记录变更
        var change = new ApprovalTaskAssigneeChange(
            tenantId,
            instanceId,
            task.NodeId,
            request.TargetAssigneeValue,
            AssigneeChangeType.Change,
            operatorUserId,
            _idGeneratorAccessor.NextId(),
            taskId.Value,
            request.Comment);
        await _assigneeChangeRepository.AddAsync(change, cancellationToken);

        // 更新任务处理人
        task.Transfer(request.TargetAssigneeValue);
        await _taskRepository.UpdateAsync(task, cancellationToken);

        // 记录历史事件
        var changeEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            task.NodeId,
            task.NodeId,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(changeEvent, cancellationToken);
    }
}





