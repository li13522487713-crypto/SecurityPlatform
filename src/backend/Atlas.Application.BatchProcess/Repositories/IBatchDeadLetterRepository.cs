using Atlas.Domain.BatchProcess.Entities;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Application.BatchProcess.Repositories;

public interface IBatchDeadLetterRepository
{
    Task AddBatchAsync(IReadOnlyList<BatchDeadLetter> entities, CancellationToken cancellationToken);
    Task UpdateAsync(BatchDeadLetter entity, CancellationToken cancellationToken);
    Task UpdateBatchAsync(IReadOnlyList<BatchDeadLetter> entities, CancellationToken cancellationToken);
    Task<BatchDeadLetter?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BatchDeadLetter>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken);
    Task<(IReadOnlyList<BatchDeadLetter> Items, int TotalCount)> QueryPageAsync(
        long? jobExecutionId,
        DeadLetterStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<BatchDeadLetter>> GetPendingByJobExecutionIdAsync(long jobExecutionId, int limit, CancellationToken cancellationToken);
}
