using Atlas.Application.BatchProcess.Repositories;
using Atlas.Domain.BatchProcess.Entities;
using Atlas.Domain.BatchProcess.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.BatchProcess.Repositories;

public sealed class BatchDeadLetterRepository : IBatchDeadLetterRepository
{
    private readonly ISqlSugarClient _db;

    public BatchDeadLetterRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddBatchAsync(IReadOnlyList<BatchDeadLetter> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(BatchDeadLetter entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateBatchAsync(IReadOnlyList<BatchDeadLetter> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return;
        await _db.Updateable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<BatchDeadLetter?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BatchDeadLetter>()
            .Where(x => x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatchDeadLetter>> GetByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<BatchDeadLetter>();
        }

        return await _db.Queryable<BatchDeadLetter>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<BatchDeadLetter> Items, int TotalCount)> QueryPageAsync(
        long? jobExecutionId,
        DeadLetterStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<BatchDeadLetter>();

        if (jobExecutionId.HasValue)
        {
            query = query.Where(x => x.JobExecutionId == jobExecutionId.Value);
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

    public async Task<IReadOnlyList<BatchDeadLetter>> GetPendingByJobExecutionIdAsync(long jobExecutionId, int limit, CancellationToken cancellationToken)
    {
        return await _db.Queryable<BatchDeadLetter>()
            .Where(x => x.JobExecutionId == jobExecutionId && x.Status == DeadLetterStatus.Pending)
            .OrderBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
