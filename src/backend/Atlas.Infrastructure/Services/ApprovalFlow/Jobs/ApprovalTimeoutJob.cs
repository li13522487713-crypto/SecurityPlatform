using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
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
        var reminders = await _db.Queryable<Domain.Approval.Entities.ApprovalTimeoutReminder>()
            .Where(x => !x.IsCompleted && x.ExpectedCompleteTime <= now)
            .OrderBy(x => x.ExpectedCompleteTime)
            .ToListAsync(cancellationToken);

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

                var task = await _taskRepository.GetByIdAsync(reminder.TenantId, reminder.TaskId.Value, cancellationToken);
                if (task == null || task.Status != ApprovalTaskStatus.Pending)
                {
                    // 任务已完成或取消，标记提醒为无效或已处理
                    reminder.MarkCompleted();
                    await _reminderRepository.UpdateAsync(reminder, cancellationToken);
                    continue;
                }

                // 获取节点配置
                var instance = await _instanceRepository.GetByIdAsync(reminder.TenantId, reminder.InstanceId, cancellationToken);
                if (instance == null) continue;

                var flowDef = await _flowRepository.GetByIdAsync(reminder.TenantId, instance.DefinitionId, cancellationToken);
                if (flowDef == null) continue;

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
                        // 跳过当前节点，直接进入下一节点
                        // 相当于自动通过，但不记录为 Approved? 或者记录为 Skipped?
                        // 这里简单处理为自动通过
                        task.Approve(0, "超时自动跳过", now);
                        await _taskRepository.UpdateAsync(task, cancellationToken);
                        await _flowEngine.AdvanceFlowAsync(reminder.TenantId, instance, flowDefinition, node.Id, cancellationToken);
                        await _instanceRepository.UpdateAsync(instance, cancellationToken);
                        break;

                    case TimeoutAction.None:
                    default:
                        // 仅提醒（发送通知逻辑在 ReminderJob 中处理，或者这里触发通知事件）
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
