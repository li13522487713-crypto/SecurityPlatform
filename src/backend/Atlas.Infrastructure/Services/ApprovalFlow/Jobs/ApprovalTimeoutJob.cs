using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Jobs;

/// <summary>
/// 审批超时任务（定期扫描超时提醒记录并执行操作）
/// </summary>
public sealed class ApprovalTimeoutJob
{
    private readonly ISqlSugarClient _db;
    private readonly IApprovalTimeoutReminderRepository _reminderRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly FlowEngine _flowEngine;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApprovalTimeoutJob> _logger;

    public ApprovalTimeoutJob(
        ISqlSugarClient db,
        IApprovalTimeoutReminderRepository reminderRepository,
        IApprovalTaskRepository taskRepository,
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        FlowEngine flowEngine,
        TimeProvider timeProvider,
        ILogger<ApprovalTimeoutJob> logger)
    {
        _db = db;
        _reminderRepository = reminderRepository;
        _taskRepository = taskRepository;
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _flowEngine = flowEngine;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        // 1. 查询所有已到期且未处理的超时提醒
        var reminders = await _db.Queryable<ApprovalTimeoutReminder>()
            .Where(x => !x.IsCompleted && x.ExpectedCompleteTime <= now)
            .OrderBy(x => x.ExpectedCompleteTime)
            .ToListAsync(cancellationToken);

        if (reminders.Count == 0) return;

        // 2. 批量预加载所有关联的 task（有 TaskId 的 reminder）
        var tenantId = reminders[0].TenantId;
        var taskIds = reminders
            .Where(r => r.TaskId.HasValue)
            .Select(r => r.TaskId!.Value)
            .Distinct()
            .ToList();

        var tasksById = taskIds.Count > 0
            ? (await _taskRepository.QueryByIdsAsync(tenantId, taskIds, cancellationToken))
                .ToDictionary(t => t.Id)
            : new Dictionary<long, ApprovalTask>();

        // 3. 批量预加载所有关联的 instance
        var instanceIds = reminders
            .Select(r => r.InstanceId)
            .Distinct()
            .ToList();

        var instancesById = instanceIds.Count > 0
            ? (await _instanceRepository.QueryByIdsAsync(tenantId, instanceIds, cancellationToken))
                .ToDictionary(i => i.Id)
            : new Dictionary<long, ApprovalProcessInstance>();

        // 4. 批量预加载所有关联的流程定义
        var definitionIds = instancesById.Values
            .Select(i => i.DefinitionId)
            .Distinct()
            .ToList();

        var flowDefsById = definitionIds.Count > 0
            ? (await _flowRepository.QueryByIdsAsync(tenantId, definitionIds, cancellationToken))
                .ToDictionary(f => f.Id)
            : new Dictionary<long, ApprovalFlowDefinition>();

        foreach (var reminder in reminders)
        {
            try
            {
                // 检查任务是否还在 Pending 状态
                if (!reminder.TaskId.HasValue)
                {
                    reminder.MarkCompleted();
                    await _reminderRepository.UpdateAsync(reminder, cancellationToken);
                    continue;
                }

                if (!tasksById.TryGetValue(reminder.TaskId.Value, out var task)
                    || task.Status != ApprovalTaskStatus.Pending)
                {
                    reminder.MarkCompleted();
                    await _reminderRepository.UpdateAsync(reminder, cancellationToken);
                    continue;
                }

                if (!instancesById.TryGetValue(reminder.InstanceId, out var instance))
                    continue;

                if (!flowDefsById.TryGetValue(instance.DefinitionId, out var flowDef))
                    continue;

                var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);
                var node = flowDefinition.GetNodeById(reminder.NodeId);
                if (node == null) continue;

                // 执行超时动作
                switch (node.TimeoutAction)
                {
                    case TimeoutAction.AutoApprove:
                        task.Approve(0, "超时自动通过", now); // 0 表示系统
                        await _taskRepository.UpdateAsync(task, cancellationToken);
                        // 推进流程
                        await _flowEngine.AdvanceFlowAsync(reminder.TenantId, instance, flowDefinition, node.Id, cancellationToken);
                        await _instanceRepository.UpdateAsync(instance, cancellationToken);
                        break;

                    case TimeoutAction.AutoReject:
                        task.Reject(0, "超时自动驳回", now);
                        await _taskRepository.UpdateAsync(task, cancellationToken);
                        instance.MarkRejected(now);
                        await _instanceRepository.UpdateAsync(instance, cancellationToken);
                        break;

                    case TimeoutAction.AutoSkip:
                        task.Approve(0, "超时自动跳过", now);
                        await _taskRepository.UpdateAsync(task, cancellationToken);
                        await _flowEngine.AdvanceFlowAsync(reminder.TenantId, instance, flowDefinition, node.Id, cancellationToken);
                        await _instanceRepository.UpdateAsync(instance, cancellationToken);
                        break;

                    case TimeoutAction.None:
                    default:
                        break;
                }

                reminder.MarkCompleted();
                await _reminderRepository.UpdateAsync(reminder, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理超时提醒失败: {ReminderId}", reminder.Id);
            }
        }
    }
}
