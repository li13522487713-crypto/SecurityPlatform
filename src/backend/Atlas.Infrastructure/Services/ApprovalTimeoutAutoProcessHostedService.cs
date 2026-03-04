using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Background service that scans for timed-out approval tasks and automatically
/// processes them based on the node's TimeoutAction configuration (auto-approve,
/// auto-reject, or auto-skip). Runs every 2 minutes.
/// </summary>
public sealed class ApprovalTimeoutAutoProcessHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalTimeoutAutoProcessHostedService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(2);

    public ApprovalTimeoutAutoProcessHostedService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalTimeoutAutoProcessHostedService> logger,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Approval timeout auto-processing service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndAutoProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for timed-out approval tasks.");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }

        _logger.LogInformation("Approval timeout auto-processing service stopped.");
    }

    private async Task ScanAndAutoProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var reminderRepository = sp.GetRequiredService<IApprovalTimeoutReminderRepository>();
        var taskRepository = sp.GetRequiredService<IApprovalTaskRepository>();
        var instanceRepository = sp.GetRequiredService<IApprovalInstanceRepository>();
        var flowRepository = sp.GetRequiredService<IApprovalFlowRepository>();
        var runtimeCommandService = sp.GetRequiredService<IApprovalRuntimeCommandService>();
        var tenantProvider = sp.GetRequiredService<ITenantProvider>();

        var now = _timeProvider.GetUtcNow();
        var tenantId = tenantProvider.GetTenantId();

        // Get all pending reminders that have passed their expected completion time
        var overdueReminders = await reminderRepository.GetPendingRemindersAsync(tenantId, now, cancellationToken);
        if (overdueReminders.Count == 0) return;

        // 批量预加载所有关联的 task
        var taskIds = overdueReminders
            .Where(r => r.TaskId.HasValue)
            .Select(r => r.TaskId!.Value)
            .Distinct()
            .ToList();

        var tasksById = taskIds.Count > 0
            ? (await taskRepository.QueryByIdsAsync(tenantId, taskIds, cancellationToken))
                .ToDictionary(t => t.Id)
            : new Dictionary<long, ApprovalTask>();

        // 批量预加载所有关联的 instance
        var instanceIds = overdueReminders.Select(r => r.InstanceId).Distinct().ToList();
        var instancesById = (await instanceRepository.QueryByIdsAsync(tenantId, instanceIds, cancellationToken))
            .ToDictionary(i => i.Id);

        // 批量预加载所有关联的流程定义
        var definitionIds = instancesById.Values.Select(i => i.DefinitionId).Distinct().ToList();
        var flowDefsById = (await flowRepository.QueryByIdsAsync(tenantId, definitionIds, cancellationToken))
            .ToDictionary(f => f.Id);

        // Group by instance to avoid repeated flow definition parsing
        var remindersByInstance = overdueReminders.GroupBy(r => r.InstanceId);

        foreach (var instanceGroup in remindersByInstance)
        {
            try
            {
                var instanceId = instanceGroup.Key;
                if (!instancesById.TryGetValue(instanceId, out var instance)
                    || instance.Status != ApprovalInstanceStatus.Running)
                {
                    continue;
                }

                if (!flowDefsById.TryGetValue(instance.DefinitionId, out var flowDef)) continue;

                var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);

                // 收集本轮已完成的 reminder，批量更新
                var completedReminders = new List<ApprovalTimeoutReminder>();

                foreach (var reminder in instanceGroup)
                {
                    if (!reminder.TaskId.HasValue) continue;

                    if (!tasksById.TryGetValue(reminder.TaskId.Value, out var task)
                        || task.Status != ApprovalTaskStatus.Pending) continue;

                    // Look up the node's timeout action
                    var node = flowDefinition.GetNodeById(task.NodeId);
                    if (node == null || !node.TimeoutEnabled || node.TimeoutAction == TimeoutAction.None)
                    {
                        continue;
                    }

                    var maxReminders = node.MaxReminderCount ?? 3;
                    if (reminder.ReminderCount < maxReminders)
                    {
                        continue;
                    }

                    try
                    {
                        switch (node.TimeoutAction)
                        {
                            case TimeoutAction.AutoApprove:
                                _logger.LogInformation(
                                    "Auto-approving timed-out task: Instance={InstanceId}, Task={TaskId}, Node={NodeId}",
                                    instanceId, task.Id, task.NodeId);
                                await runtimeCommandService.ApproveTaskAsync(
                                    tenantId, task.Id, long.Parse(task.AssigneeValue), "系统自动通过（超时）", cancellationToken);
                                break;

                            case TimeoutAction.AutoReject:
                                _logger.LogInformation(
                                    "Auto-rejecting timed-out task: Instance={InstanceId}, Task={TaskId}, Node={NodeId}",
                                    instanceId, task.Id, task.NodeId);
                                await runtimeCommandService.RejectTaskAsync(
                                    tenantId, task.Id, long.Parse(task.AssigneeValue), "系统自动驳回（超时）", cancellationToken);
                                break;

                            case TimeoutAction.AutoSkip:
                                _logger.LogInformation(
                                    "Auto-skipping timed-out task: Instance={InstanceId}, Task={TaskId}, Node={NodeId}",
                                    instanceId, task.Id, task.NodeId);
                                await runtimeCommandService.ApproveTaskAsync(
                                    tenantId, task.Id, long.Parse(task.AssigneeValue), "系统自动跳过（超时）", cancellationToken);
                                break;
                        }

                        reminder.MarkCompleted();
                        completedReminders.Add(reminder);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to auto-process timed-out task: Instance={InstanceId}, Task={TaskId}, Action={Action}",
                            instanceId, task.Id, node.TimeoutAction);
                    }
                }

                // 批量更新本实例组内所有已完成的 reminder
                if (completedReminders.Count > 0)
                {
                    await reminderRepository.UpdateRangeAsync(completedReminders, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timeout for instance {InstanceId}", instanceGroup.Key);
            }
        }
    }
}
