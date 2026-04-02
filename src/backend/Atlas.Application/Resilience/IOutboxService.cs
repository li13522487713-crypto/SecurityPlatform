using Atlas.Core.Messaging;

namespace Atlas.Application.Resilience;

public interface IOutboxService
{
    Task<long> EnqueueAsync(
        string eventType,
        string payloadJson,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkProcessedAsync(long id, CancellationToken cancellationToken);

    Task MarkFailedAsync(long id, string errorMessage, CancellationToken cancellationToken);
}
