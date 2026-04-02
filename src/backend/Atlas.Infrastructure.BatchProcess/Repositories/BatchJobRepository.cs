using Atlas.Application.BatchProcess.Repositories;
using Atlas.Domain.BatchProcess.Entities;
using Atlas.Domain.BatchProcess.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.BatchProcess.Repositories;

public sealed class BatchJobRepository : IBatchJobRepository
{
    private readonly ISqlSugarClient _db;

    public BatchJobRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(BatchJobDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(BatchJobDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<BatchJobDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BatchJobDefinition>()
            .Where(x => x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<BatchJobDefinition> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        BatchJobStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<BatchJobDefinition>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<long> AddExecutionAsync(BatchJobExecution entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateExecutionAsync(BatchJobExecution entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<BatchJobExecution?> GetExecutionByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BatchJobExecution>()
            .Where(x => x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<BatchJobExecution> Items, int TotalCount)> QueryExecutionPageAsync(
        long jobDefinitionId,
        int pageIndex,
        int pageSize,
        JobExecutionStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<BatchJobExecution>()
            .Where(x => x.JobDefinitionId == jobDefinitionId);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task AddShardsAsync(IReadOnlyList<ShardExecution> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateShardAsync(ShardExecution entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateShardsAsync(IReadOnlyList<ShardExecution> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Updateable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ShardExecution?> GetShardByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ShardExecution>()
            .Where(x => x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShardExecution>> GetShardsByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ShardExecution>();
        }

        return await _db.Queryable<ShardExecution>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShardExecution>> GetShardsByExecutionIdAsync(long jobExecutionId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ShardExecution>()
            .Where(x => x.JobExecutionId == jobExecutionId)
            .OrderBy(x => x.ShardIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShardExecution>> GetFailedShardsByExecutionIdAsync(long jobExecutionId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ShardExecution>()
            .Where(x => x.JobExecutionId == jobExecutionId && x.Status == ShardExecutionStatus.Failed)
            .OrderBy(x => x.ShardIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task AddBatchesAsync(IReadOnlyList<BatchExecution> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateBatchAsync(BatchExecution entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatchExecution>> GetBatchesByShardIdAsync(long shardExecutionId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BatchExecution>()
            .Where(x => x.ShardExecutionId == shardExecutionId)
            .OrderBy(x => x.BatchIndex)
            .ToListAsync(cancellationToken);
    }
}
