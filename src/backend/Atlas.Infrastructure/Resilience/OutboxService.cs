using Atlas.Application.Resilience;
using Atlas.Core.Abstractions;
using Atlas.Core.Messaging;
using SqlSugar;

namespace Atlas.Infrastructure.Resilience;

public sealed class OutboxService : IOutboxService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public OutboxService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<long> EnqueueAsync(
        string eventType,
        string payloadJson,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var row = new OutboxMessage
        {
            Id = _idGen.NextId(),
            EventType = eventType,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            RetryCount = 0,
            MaxRetries = 3
        };

        await _db.Insertable(row).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        return row.Id;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var take = batchSize < 1 ? 1 : batchSize;
        var now = DateTime.UtcNow;
        var list = await _db.Queryable<OutboxMessage>()
            .Where(x => !x.IsProcessed && (x.NextRetryAt == null || x.NextRetryAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return list;
    }

    public async Task MarkProcessedAsync(long id, CancellationToken cancellationToken)
    {
        await _db.Updateable<OutboxMessage>()
            .SetColumns(x => new OutboxMessage
            {
                IsProcessed = true,
                ProcessedAt = DateTime.UtcNow
            })
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(long id, string errorMessage, CancellationToken cancellationToken)
    {
        var row = await _db.Queryable<OutboxMessage>()
            .FirstAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        var nextRetry = row.RetryCount + 1;
        var delaySeconds = Math.Min(300, (int)Math.Pow(2, nextRetry));
        var nextAt = DateTime.UtcNow.AddSeconds(delaySeconds);
        var terminal = nextRetry >= row.MaxRetries;

        await _db.Updateable<OutboxMessage>()
            .SetColumns(x => new OutboxMessage
            {
                RetryCount = nextRetry,
                ErrorMessage = errorMessage,
                NextRetryAt = terminal ? null : nextAt,
                IsProcessed = terminal,
                ProcessedAt = terminal ? DateTime.UtcNow : null
            })
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
