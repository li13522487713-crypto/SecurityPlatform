using Atlas.Application.System.Models;
using Atlas.Core.Models;

namespace Atlas.Application.System.Abstractions;

public interface IScheduledJobService
{
    /// <summary>获取所有注册的 Recurring Job 列表</summary>
    Task<PagedResult<ScheduledJobDto>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken ct = default);

    /// <summary>立即触发一次任务</summary>
    Task TriggerAsync(string jobId, CancellationToken ct = default);

    /// <summary>启用/暂停任务</summary>
    Task SetEnabledAsync(string jobId, bool enabled, CancellationToken ct = default);
}
