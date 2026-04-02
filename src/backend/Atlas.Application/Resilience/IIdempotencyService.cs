namespace Atlas.Application.Resilience;

public sealed record IdempotencyCheckResult(bool IsProcessed, string? CachedResponseJson);

public interface IIdempotencyService
{
    Task<IdempotencyCheckResult> IsProcessedAsync(
        string tenantId,
        string userId,
        string apiName,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task RecordAsync(
        string tenantId,
        string userId,
        string apiName,
        string idempotencyKey,
        string responseJson,
        CancellationToken cancellationToken);
}
