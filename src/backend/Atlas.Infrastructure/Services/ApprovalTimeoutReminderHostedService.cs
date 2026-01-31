using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批超时提醒后台服务（定时扫描并发送提醒）
/// </summary>
public sealed class ApprovalTimeoutReminderHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalTimeoutReminderHostedService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(5); // 每5分钟扫描一次

    public ApprovalTimeoutReminderHostedService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalTimeoutReminderHostedService> logger,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("审批超时提醒服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndSendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描超时提醒时发生错误");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }

        _logger.LogInformation("审批超时提醒服务已停止");
    }

    private async Task ScanAndSendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var reminderRepository = scope.ServiceProvider.GetRequiredService<IApprovalTimeoutReminderRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>();
        var instanceRepository = scope.ServiceProvider.GetRequiredService<IApprovalInstanceRepository>();
        var notificationService = scope.ServiceProvider.GetService<IApprovalNotificationService>();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

        if (notificationService == null)
        {
            return; // 如果没有配置通知服务，跳过
        }

        var currentTime = _timeProvider.GetUtcNow();
        
        // 获取所有租户的待处理提醒（简化实现：假设单租户或通过其他方式获取租户列表）
        // TODO: 如果需要多租户支持，需要遍历所有租户
        var tenantId = tenantProvider.GetTenantId();
        var pendingReminders = await reminderRepository.GetPendingRemindersAsync(tenantId, currentTime, cancellationToken);
        if (pendingReminders.Count == 0)
        {
            return;
        }

        var taskIds = pendingReminders
            .Where(r => r.TaskId.HasValue)
            .Select(r => r.TaskId!.Value)
            .Distinct()
            .ToArray();
        var tasks = await taskRepository.QueryByIdsAsync(tenantId, taskIds, cancellationToken);
        var taskMap = tasks.ToDictionary(x => x.Id);

        var instanceIds = pendingReminders
            .Select(r => r.InstanceId)
            .Distinct()
            .ToArray();
        var instances = await instanceRepository.QueryByIdsAsync(tenantId, instanceIds, cancellationToken);
        var instanceMap = instances.ToDictionary(x => x.Id);

        var remindersToUpdate = new List<ApprovalTimeoutReminder>();

        foreach (var reminder in pendingReminders)
        {
            try
            {
                // 检查任务是否已完成
                var isTaskCompleted = false;
                if (reminder.TaskId.HasValue)
                {
                    if (taskMap.TryGetValue(reminder.TaskId.Value, out var task) &&
                        task.Status != ApprovalTaskStatus.Pending)
                    {
                        isTaskCompleted = true;
                    }
                }

                // 检查流程实例是否已结束
                instanceMap.TryGetValue(reminder.InstanceId, out var instance);
                var isInstanceEnded = instance == null || 
                    instance.Status == ApprovalInstanceStatus.Completed ||
                    instance.Status == ApprovalInstanceStatus.Rejected ||
                    instance.Status == ApprovalInstanceStatus.Canceled;

                if (isTaskCompleted || isInstanceEnded)
                {
                    // 标记提醒为已完成
                    reminder.MarkCompleted();
                    remindersToUpdate.Add(reminder);
                    continue;
                }

                // 检查是否需要发送提醒（根据提醒间隔和最大提醒次数）
                var shouldRemind = ShouldSendReminder(reminder, currentTime);
                if (!shouldRemind)
                {
                    continue;
                }

                // 发送提醒
                if (instance != null)
                {
                    ApprovalTask? task = null;
                    if (reminder.TaskId.HasValue)
                    {
                        taskMap.TryGetValue(reminder.TaskId.Value, out task);
                    }

                    await notificationService.NotifyAsync(
                        tenantId,
                        ApprovalNotificationEventType.TimeoutReminder,
                        instance,
                        task,
                        new[] { reminder.RecipientUserId },
                        cancellationToken);

                    // 记录提醒
                    reminder.RecordReminder(currentTime);
                    remindersToUpdate.Add(reminder);

                    _logger.LogInformation(
                        "已发送超时提醒：InstanceId={InstanceId}, TaskId={TaskId}, RecipientUserId={RecipientUserId}",
                        reminder.InstanceId, reminder.TaskId, reminder.RecipientUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "处理超时提醒失败：ReminderId={ReminderId}, InstanceId={InstanceId}",
                    reminder.Id, reminder.InstanceId);
            }
        }

        if (remindersToUpdate.Count > 0)
        {
            await reminderRepository.UpdateRangeAsync(remindersToUpdate, cancellationToken);
        }
    }

    /// <summary>
    /// 判断是否应该发送提醒
    /// </summary>
    private static bool ShouldSendReminder(ApprovalTimeoutReminder reminder, DateTimeOffset currentTime)
    {
        // 如果还没有发送过提醒，且已超时，则发送
        if (reminder.ReminderCount == 0)
        {
            return true;
        }

        // 如果已超过最大提醒次数，不再发送
        // TODO: 从节点配置中获取 MaxReminderCount
        const int defaultMaxReminderCount = 3;
        if (reminder.ReminderCount >= defaultMaxReminderCount)
        {
            return false;
        }

        // 检查是否达到提醒间隔
        // TODO: 从节点配置中获取 ReminderIntervalHours
        const int defaultReminderIntervalHours = 24;
        if (reminder.LastReminderAt.HasValue)
        {
            var timeSinceLastReminder = currentTime - reminder.LastReminderAt.Value;
            if (timeSinceLastReminder.TotalHours < defaultReminderIntervalHours)
            {
                return false;
            }
        }

        return true;
    }
}
