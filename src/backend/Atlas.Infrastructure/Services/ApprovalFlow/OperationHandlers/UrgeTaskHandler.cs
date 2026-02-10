using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.OperationHandlers;

/// <summary>
/// 催办任务处理器
/// </summary>
public sealed class UrgeTaskHandler : IApprovalOperationHandler
{
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalHistoryRepository _historyRepository;
    private readonly IApprovalNotificationService? _notificationService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public UrgeTaskHandler(
        IApprovalTaskRepository taskRepository,
        IApprovalHistoryRepository historyRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IApprovalNotificationService? notificationService = null)
    {
        _taskRepository = taskRepository;
        _historyRepository = historyRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _notificationService = notificationService;
    }

    public ApprovalOperationType SupportedOperationType => ApprovalOperationType.Urge;

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

        // 发送催办通知
        if (_notificationService != null && long.TryParse(task.AssigneeValue, out var assigneeId))
        {
            // 这里复用 TaskCreated 事件或者新增 TaskUrged 事件
            // 简单起见，假设 NotificationService 支持自定义消息
            // 或者扩展 ApprovalNotificationEventType.TaskUrged
        }

        // 记录催办历史
        var historyEvent = new ApprovalHistoryEvent(
            tenantId,
            instanceId,
            ApprovalHistoryEventType.TaskUrged,
            task.NodeId,
            request.Comment ?? "催办",
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }
}
