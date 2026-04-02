using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.BatchProcess.Abstractions;

public interface ICheckpointService
{
    Task<long> SaveAsync(long shardExecutionId, string checkpointKey, string processedUpTo, long processedCount, TenantId tenantId, CancellationToken cancellationToken);
    Task<CheckpointInfo?> GetLatestAsync(long shardExecutionId, CancellationToken cancellationToken);
}
