using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.BatchProcess.Abstractions;

/// <summary>
/// 分片恢复服务：从最近的 checkpoint 恢复分片执行。
/// </summary>
public interface IShardRecoveryService
{
    Task<RecoveryResult> RecoverShardAsync(long shardExecutionId, TenantId tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecoveryResult>> RecoverShardsAsync(
        IReadOnlyCollection<long> shardExecutionIds,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
