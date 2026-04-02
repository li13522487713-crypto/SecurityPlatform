using Atlas.Domain.BatchProcess.Entities;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Repositories;

public interface IBatchJobRepository
{
    Task<long> AddAsync(BatchJobDefinition entity, CancellationToken cancellationToken);
    Task UpdateAsync(BatchJobDefinition entity, CancellationToken cancellationToken);
    Task<BatchJobDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<BatchJobDefinition> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        BatchJobStatus? status,
        CancellationToken cancellationToken);

    Task<long> AddExecutionAsync(BatchJobExecution entity, CancellationToken cancellationToken);
    Task UpdateExecutionAsync(BatchJobExecution entity, CancellationToken cancellationToken);
    Task<BatchJobExecution?> GetExecutionByIdAsync(long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<BatchJobExecution> Items, int TotalCount)> QueryExecutionPageAsync(
        long jobDefinitionId,
        int pageIndex,
        int pageSize,
        JobExecutionStatus? status,
        CancellationToken cancellationToken);

    Task AddShardsAsync(IReadOnlyList<ShardExecution> entities, CancellationToken cancellationToken);
    Task UpdateShardAsync(ShardExecution entity, CancellationToken cancellationToken);
    Task UpdateShardsAsync(IReadOnlyList<ShardExecution> entities, CancellationToken cancellationToken);
    Task<ShardExecution?> GetShardByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShardExecution>> GetShardsByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShardExecution>> GetShardsByExecutionIdAsync(long jobExecutionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShardExecution>> GetFailedShardsByExecutionIdAsync(long jobExecutionId, CancellationToken cancellationToken);

    Task AddBatchesAsync(IReadOnlyList<BatchExecution> entities, CancellationToken cancellationToken);
    Task UpdateBatchAsync(BatchExecution entity, CancellationToken cancellationToken);
    Task<IReadOnlyList<BatchExecution>> GetBatchesByShardIdAsync(long shardExecutionId, CancellationToken cancellationToken);
}
