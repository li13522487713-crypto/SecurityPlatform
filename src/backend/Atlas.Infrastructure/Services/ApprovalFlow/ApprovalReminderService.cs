using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 催办服务实现
/// </summary>
public sealed class ApprovalReminderService : IApprovalReminderService
{
    private readonly IApprovalReminderRecordRepository _reminderRecordRepository;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalNotificationService? _notificationService;
    private readonly IIdGenerator _idGenerator;

    public ApprovalReminderService(
        IApprovalReminderRecordRepository reminderRecordRepository,
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalNotificationService? notificationService,
        IIdGenerator idGenerator)
    {
        _reminderRecordRepository = reminderRecordRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _notificationService = notificationService;
        _idGenerator = idGenerator;
    }

    public async Task SendReminderAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long reminderUserId,
        long recipientUserId,
        string reminderMessage,
        CancellationToken cancellationToken)
    {
        // 验证流程实例是否存在
        var instance = await _instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
        if (instance == null)
        {
            throw new Core.Exceptions.BusinessException("INSTANCE_NOT_FOUND", "流程实例不存在");
        }

        // 如果指定了任务ID，验证任务是否存在
        if (taskId.HasValue)
        {
            var task = await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken);
            if (task == null || task.InstanceId != instanceId)
            {
                throw new Core.Exceptions.BusinessException("TASK_NOT_FOUND", "任务不存在");
            }
        }

        // 创建催办记录
        var reminderRecord = new ApprovalReminderRecord(
            tenantId,
            instanceId,
            taskId,
            reminderUserId,
            recipientUserId,
            reminderMessage,
            _idGenerator.NextId());

        await _reminderRecordRepository.AddAsync(reminderRecord, cancellationToken);

        // 发送催办通知
        if (_notificationService != null)
        {
            var task = taskId.HasValue
                ? await _taskRepository.GetByIdAsync(tenantId, taskId.Value, cancellationToken)
                : null;

            await _notificationService.NotifyAsync(
                tenantId,
                ApprovalNotificationEventType.Reminder,
                instance,
                task,
                new[] { recipientUserId },
                cancellationToken);
        }
    }
}
