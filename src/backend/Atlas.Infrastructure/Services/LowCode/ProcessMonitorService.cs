using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class ProcessMonitorService : IProcessMonitorService
{
    private readonly ISqlSugarClient _db;

    public ProcessMonitorService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<ProcessMonitorDashboard> GetDashboardAsync(
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var todayOffset = new DateTimeOffset(today, TimeSpan.Zero);

        // Active instances
        var activeInstances = await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Status == ApprovalInstanceStatus.Running)
            .CountAsync(cancellationToken);

        // Completed today
        var completedToday = await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.Status == ApprovalInstanceStatus.Completed
                && x.EndedAt >= todayOffset)
            .CountAsync(cancellationToken);

        // Rejected today
        var rejectedToday = await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.Status == ApprovalInstanceStatus.Rejected
                && x.EndedAt >= todayOffset)
            .CountAsync(cancellationToken);

        // Total definitions
        var totalDefinitions = await _db.Queryable<ApprovalFlowDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);

        // Pending tasks
        var pendingTasks = await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Status == ApprovalTaskStatus.Pending)
            .CountAsync(cancellationToken);

        // Overdue tasks (pending tasks older than 7 days as a heuristic)
        var sevenDaysAgoOverdue = DateTimeOffset.UtcNow.AddDays(-7);
        var overdueTasks = await _db.Queryable<ApprovalTask>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.Status == ApprovalTaskStatus.Pending
                && x.CreatedAt < sevenDaysAgoOverdue)
            .CountAsync(cancellationToken);

        // Average completion time (last 30 days)
        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var completedInstances = await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.Status == ApprovalInstanceStatus.Completed
                && x.EndedAt >= thirtyDaysAgo
                && x.EndedAt != null)
            .Select(x => new { x.StartedAt, x.EndedAt })
            .ToListAsync(cancellationToken);

        var avgMinutes = completedInstances.Count > 0
            ? completedInstances.Average(x => (x.EndedAt!.Value - x.StartedAt).TotalMinutes)
            : 0;

        // Bottlenecks: nodes with most pending tasks
        var bottlenecks = new List<ProcessNodeBottleneck>();

        // Daily stats for last 7 days
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var recentInstances = await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.StartedAt >= sevenDaysAgo)
            .Select(x => new { x.StartedAt, x.EndedAt, x.Status })
            .ToListAsync(cancellationToken);

        var dailyStats = Enumerable.Range(0, 7)
            .Select(i => DateTimeOffset.UtcNow.AddDays(-6 + i).Date)
            .Select(date =>
            {
                var dateOffset = new DateTimeOffset(date, TimeSpan.Zero);
                var nextDate = dateOffset.AddDays(1);
                var dayInstances = recentInstances.Where(x => x.StartedAt >= dateOffset && x.StartedAt < nextDate);
                var dayCompleted = recentInstances.Where(x =>
                    x.Status == ApprovalInstanceStatus.Completed && x.EndedAt >= dateOffset && x.EndedAt < nextDate);
                var dayRejected = recentInstances.Where(x =>
                    x.Status == ApprovalInstanceStatus.Rejected && x.EndedAt >= dateOffset && x.EndedAt < nextDate);

                return new ProcessDailyStats(
                    date.ToString("yyyy-MM-dd"),
                    dayInstances.Count(),
                    dayCompleted.Count(),
                    dayRejected.Count());
            })
            .ToList();

        return new ProcessMonitorDashboard(
            activeInstances,
            completedToday,
            rejectedToday,
            totalDefinitions,
            pendingTasks,
            overdueTasks,
            Math.Round(avgMinutes, 1),
            bottlenecks,
            dailyStats);
    }

    public async Task<ProcessInstanceTrace?> GetInstanceTraceAsync(
        TenantId tenantId, long instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == instanceId)
            .FirstAsync(cancellationToken);

        if (instance is null) return null;

        // Get flow name
        var flow = await _db.Queryable<ApprovalFlowDefinition>()
            .Where(x => x.Id == instance.DefinitionId)
            .Select(x => new { x.Name })
            .FirstAsync(cancellationToken);

        // Get node executions
        var nodeExecutions = await _db.Queryable<ApprovalNodeExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);

        var nodeTraces = nodeExecutions.Select(ne =>
        {
            var duration = ne.CompletedAt.HasValue
                ? (ne.CompletedAt.Value - ne.StartedAt).TotalMinutes
                : (double?)null;

            return new ProcessNodeTrace(
                ne.NodeId,
                ne.NodeId,
                "UserTask",
                ne.Status.ToString(),
                ne.StartedAt,
                ne.CompletedAt,
                duration.HasValue ? Math.Round(duration.Value, 1) : null,
                null,
                null);
        }).ToList();

        return new ProcessInstanceTrace(
            instance.Id.ToString(),
            flow?.Name ?? "未知流程",
            instance.Status.ToString(),
            instance.InitiatorUserId.ToString(),
            instance.StartedAt,
            instance.EndedAt,
            nodeTraces);
    }
}
