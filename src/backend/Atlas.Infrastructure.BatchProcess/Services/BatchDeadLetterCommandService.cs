using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;

namespace Atlas.Infrastructure.BatchProcess.Services;

public sealed class BatchDeadLetterCommandService : IBatchDeadLetterCommandService
{
    private readonly IBatchDeadLetterRepository _repository;

    public BatchDeadLetterCommandService(IBatchDeadLetterRepository repository)
    {
        _repository = repository;
    }

    public async Task RetryAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "死信记录不存在");

        if (entity.Status != DeadLetterStatus.Pending)
        {
            throw new BusinessException("INVALID_STATE", "仅待处理的死信可以重试");
        }

        entity.MarkRetrying();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task RetryBatchAsync(IReadOnlyList<long> ids, TenantId tenantId, CancellationToken cancellationToken)
    {
        var uniqueIds = ids.Distinct().ToArray();
        if (uniqueIds.Length == 0) return;

        var entities = await _repository.GetByIdsAsync(uniqueIds, cancellationToken);
        var entitiesToUpdate = new List<Atlas.Domain.BatchProcess.Entities.BatchDeadLetter>(entities.Count);

        foreach (var entity in entities)
        {
            if (entity.Status != DeadLetterStatus.Pending) continue;
            entity.MarkRetrying();
            entitiesToUpdate.Add(entity);
        }

        if (entitiesToUpdate.Count > 0)
        {
            await _repository.UpdateBatchAsync(entitiesToUpdate, cancellationToken);
        }
    }

    public async Task AbandonAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "死信记录不存在");

        if (entity.Status == DeadLetterStatus.Resolved || entity.Status == DeadLetterStatus.Abandoned)
        {
            throw new BusinessException("INVALID_STATE", "已解决或已放弃的死信无法再次操作");
        }

        entity.MarkAbandoned();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task AbandonBatchAsync(IReadOnlyList<long> ids, TenantId tenantId, CancellationToken cancellationToken)
    {
        var uniqueIds = ids.Distinct().ToArray();
        if (uniqueIds.Length == 0) return;

        var entities = await _repository.GetByIdsAsync(uniqueIds, cancellationToken);
        var entitiesToUpdate = new List<Atlas.Domain.BatchProcess.Entities.BatchDeadLetter>(entities.Count);

        foreach (var entity in entities)
        {
            if (entity.Status == DeadLetterStatus.Resolved || entity.Status == DeadLetterStatus.Abandoned) continue;
            entity.MarkAbandoned();
            entitiesToUpdate.Add(entity);
        }

        if (entitiesToUpdate.Count > 0)
        {
            await _repository.UpdateBatchAsync(entitiesToUpdate, cancellationToken);
        }
    }
}
