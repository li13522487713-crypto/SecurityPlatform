using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Domain.Approval.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Jobs;

/// <summary>
/// 审批提醒任务（处理催办、超时提醒通知）
/// </summary>
public sealed class ApprovalReminderJob
{
    private readonly IApprovalTimeoutReminderRepository _reminderRepository;
    private readonly IApprovalNotificationService _notificationService;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApprovalReminderJob> _logger;

    public ApprovalReminderJob(
        IApprovalTimeoutReminderRepository reminderRepository,
        IApprovalNotificationService notificationService,
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        TimeProvider timeProvider,
        ILogger<ApprovalReminderJob> logger)
    {
        _reminderRepository = reminderRepository;
        _notificationService = notificationService;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // 逻辑：扫描所有未处理且未达到最大提醒次数的记录
        // 这里简化实现，假设 Reminder 表有 NextRemindTime 字段
        // 实际实现需要更复杂的提醒策略（间隔、次数）
        await Task.CompletedTask; 
    }
}
