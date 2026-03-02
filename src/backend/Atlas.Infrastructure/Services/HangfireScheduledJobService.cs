using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Hangfire;
using Hangfire.Storage;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 基于 Hangfire API 的定时任务管理服务
/// </summary>
public sealed class HangfireScheduledJobService : IScheduledJobService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly JobStorage _jobStorage;

    public HangfireScheduledJobService(
        IRecurringJobManager recurringJobManager,
        JobStorage jobStorage)
    {
        _recurringJobManager = recurringJobManager;
        _jobStorage = jobStorage;
    }

    public Task<PagedResult<ScheduledJobDto>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken ct = default)
    {
        using var connection = _jobStorage.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();

        var dtos = recurringJobs.Select(j => new ScheduledJobDto(
            Id: j.Id,
            Name: j.Id,
            CronExpression: j.Cron,
            Queue: j.Queue,
            IsEnabled: !j.Removed,
            LastRunAt: j.LastExecution.HasValue
                ? (DateTimeOffset?)new DateTimeOffset(j.LastExecution.Value, TimeSpan.Zero)
                : null,
            LastRunStatus: j.LastJobState,
            NextRunAt: j.NextExecution.HasValue
                ? (DateTimeOffset?)new DateTimeOffset(j.NextExecution.Value, TimeSpan.Zero)
                : null
        )).ToList();

        var total = dtos.Count;
        var items = dtos
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<ScheduledJobDto>(items, total, pageIndex, pageSize));
    }

    public Task TriggerAsync(string jobId, CancellationToken ct = default)
    {
        RecurringJob.TriggerJob(jobId);
        return Task.CompletedTask;
    }

    public Task SetEnabledAsync(string jobId, bool enabled, CancellationToken ct = default)
    {
        // Hangfire 没有内置的暂停功能，通过调整 Cron 为空字符串实现禁用
        // 实际生产中可考虑维护一张自定义表记录启用状态
        // 此处简化：禁用时从 Recurring Jobs 中移除（可重新注册）
        if (!enabled)
        {
            RecurringJob.RemoveIfExists(jobId);
        }
        return Task.CompletedTask;
    }
}
