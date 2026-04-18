using Atlas.Application.ExternalConnectors.Models;
using Atlas.Connectors.Core.Models;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 跨 provider 的通讯录同步服务：全量基线 + 增量事件两段式。
/// 实现负责把 IExternalDirectoryProvider 拉到的部门 / 成员落到 Mirror 表，并写差异行。
/// </summary>
public interface IExternalDirectorySyncService
{
    Task<ExternalDirectorySyncJobResponse> RunFullSyncAsync(long providerId, string triggerSource, CancellationToken cancellationToken);

    Task<ExternalDirectorySyncJobResponse> ApplyIncrementalEventAsync(long providerId, ExternalDirectoryEvent evt, string triggerSource, CancellationToken cancellationToken);

    Task<ExternalDirectorySyncJobResponse?> GetJobAsync(long jobId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalDirectorySyncJobResponse>> ListRecentAsync(long providerId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalDirectorySyncDiffResponse>> ListJobDiffsAsync(long jobId, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountJobDiffsAsync(long jobId, CancellationToken cancellationToken);
}
