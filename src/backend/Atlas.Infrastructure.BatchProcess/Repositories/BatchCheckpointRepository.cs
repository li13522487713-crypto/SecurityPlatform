using Atlas.Application.BatchProcess.Repositories;
using Atlas.Domain.BatchProcess.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.BatchProcess.Repositories;

public sealed class BatchCheckpointRepository : IBatchCheckpointRepository
{
    private readonly ISqlSugarClient _db;

    public BatchCheckpointRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(BatchCheckpoint entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<BatchCheckpoint?> GetLatestByShardIdAsync(long shardExecutionId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BatchCheckpoint>()
            .Where(x => x.ShardExecutionId == shardExecutionId)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatchCheckpoint>> GetLatestByShardIdsAsync(
        IReadOnlyCollection<long> shardExecutionIds,
        CancellationToken cancellationToken)
    {
        if (shardExecutionIds.Count == 0)
        {
            return Array.Empty<BatchCheckpoint>();
        }

        var latestCheckpointIds = await _db.Queryable<BatchCheckpoint>()
            .Where(x => shardExecutionIds.Contains(x.ShardExecutionId))
            .GroupBy(x => x.ShardExecutionId)
            .Select(x => SqlFunc.AggregateMax(x.Id))
            .ToListAsync(cancellationToken);

        if (latestCheckpointIds.Count == 0)
        {
            return Array.Empty<BatchCheckpoint>();
        }

        return await _db.Queryable<BatchCheckpoint>()
            .Where(x => latestCheckpointIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatchCheckpoint>> GetByShardIdAsync(long shardExecutionId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BatchCheckpoint>()
            .Where(x => x.ShardExecutionId == shardExecutionId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}