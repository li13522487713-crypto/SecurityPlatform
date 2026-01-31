using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Operations;

/// <summary>
/// 减签操作处理器（移除当前节点的某个审批人）
/// </summary>
public sealed class RemoveAssigneeOperationHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalTaskAssigneeChangeRepository _assigneeChangeRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.RemoveAssignee;

    public RemoveAssigneeOperationHandler(
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
            throw new BusinessException("TASK_ID_REQUIRED", "减签操作需要指定任务ID");
        }

        if (string.IsNullOrEmpty(request.TargetAssigneeValue))
        {
            throw new BusinessException("TARGET_ASSIGNEE_REQUIRED", "减签操作需要指定要移除的审批人");
        }

        var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
        if (task == null)
        {
            throw new BusinessException("TASK_NOT_FOUND", "审批任务不存在");
        }

        // 获取同节点的所有任务
        var nodeTasks = await _taskRepository.GetByInstanceAndNodeAsync(tenantId, instanceId, task.NodeId, cancellationToken);
        var targetTask = nodeTasks.FirstOrDefault(t => t.AssigneeValue == request.TargetAssigneeValue && t.Status == ApprovalTaskStatus.Pending);

        if (targetTask == null)
        {
            throw new BusinessException("TARGET_TASK_NOT_FOUND", "未找到要移除的审批任务");
        }

        // 记录减签操作
        var change = new ApprovalTaskAssigneeChange(
            tenantId,
            instanceId,
            task.NodeId,
            request.TargetAssigneeValue,
            AssigneeChangeType.Remove,
            operatorUserId,
            _idGeneratorAccessor.NextId(),
            targetTask.Id,
            request.Comment);
        await _assigneeChangeRepository.AddAsync(change, cancellationToken);

        // 取消目标任务
        targetTask.Cancel();
        await _taskRepository.UpdateAsync(targetTask, cancellationToken);

        // 记录历史事件
        var removeEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.NodeAdvanced,
            null,
            task.NodeId,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(removeEvent, cancellationToken);
    }
}





