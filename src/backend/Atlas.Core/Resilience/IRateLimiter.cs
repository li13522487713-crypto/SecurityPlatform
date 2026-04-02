namespace Atlas.Core.Resilience;

public interface IRateLimiter
{
    Task<bool> TryAcquireAsync(string resource, CancellationToken ct);

    Task<RateLimitInfo> GetInfoAsync(string resource, CancellationToken ct);
}

public sealed record RateLimitInfo(int Remaining, int Limit, DateTimeOffset ResetsAt);
