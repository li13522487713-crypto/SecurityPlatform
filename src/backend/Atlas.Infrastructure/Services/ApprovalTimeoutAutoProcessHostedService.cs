using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
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

        // Group by instance to avoid repeated flow definition parsing
        var remindersByInstance = overdueReminders.GroupBy(r => r.InstanceId);

        foreach (var instanceGroup in remindersByInstance)
        {
            try
            {
                var instanceId = instanceGroup.Key;
                var instance = await instanceRepository.GetByIdAsync(tenantId, instanceId, cancellationToken);
                if (instance == null || instance.Status != ApprovalInstanceStatus.Running)
                {
                    continue; // Instance already finished
                }

                var flowDef = await flowRepository.GetByIdAsync(tenantId, instance.DefinitionId, cancellationToken);
                if (flowDef == null) continue;

                var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);

                foreach (var reminder in instanceGroup)
                {
                    if (!reminder.TaskId.HasValue) continue;

                    var task = await taskRepository.GetByIdAsync(tenantId, reminder.TaskId.Value, cancellationToken);
                    if (task == null || task.Status != ApprovalTaskStatus.Pending) continue;

                    // Look up the node's timeout action
                    var node = flowDefinition.GetNodeById(task.NodeId);
                    if (node == null || !node.TimeoutEnabled || node.TimeoutAction == TimeoutAction.None)
                    {
                        continue; // No auto-action configured
                    }

                    // Check if the max reminder count has been reached (auto-process after all reminders sent)
                    var maxReminders = node.MaxReminderCount ?? 3;
                    if (reminder.ReminderCount < maxReminders)
                    {
                        continue; // Still sending reminders, don't auto-process yet
                    }

                    try
                    {
                        switch (node.TimeoutAction)
                        {
                            case TimeoutAction.AutoApprove:
                                _logger.LogInformation(
                                    "Auto-approving timed-out task: Instance={InstanceId}, Task={TaskId}, Node={NodeId}",
                                    instanceId, task.Id, task.NodeId);
                                // Use a system user ID (0) for automatic actions
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
                                // Skip = auto-approve the task to advance the flow
                                await runtimeCommandService.ApproveTaskAsync(
                                    tenantId, task.Id, long.Parse(task.AssigneeValue), "系统自动跳过（超时）", cancellationToken);
                                break;
                        }

                        // Mark the reminder as completed after successful auto-processing
                        reminder.MarkCompleted();
                        await reminderRepository.UpdateAsync(reminder, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to auto-process timed-out task: Instance={InstanceId}, Task={TaskId}, Action={Action}",
                            instanceId, task.Id, node.TimeoutAction);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timeout for instance {InstanceId}", instanceGroup.Key);
            }
        }
    }
}
