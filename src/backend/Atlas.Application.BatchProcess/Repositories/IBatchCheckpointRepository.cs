using Atlas.Domain.BatchProcess.Entities;

namespace Atlas.Application.BatchProcess.Repositories;

public interface IBatchCheckpointRepository
{
    Task<long> AddAsync(BatchCheckpoint entity, CancellationToken cancellationToken);
    Task<BatchCheckpoint?> GetLatestByShardIdAsync(long shardExecutionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BatchCheckpoint>> GetLatestByShardIdsAsync(IReadOnlyCollection<long> shardExecutionIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<BatchCheckpoint>> GetByShardIdAsync(long shardExecutionId, CancellationToken cancellationToken);
}
