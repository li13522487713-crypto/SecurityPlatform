using System.Collections.Concurrent;
using Atlas.Core.Resilience;

namespace Atlas.Infrastructure.Resilience;

public sealed class InMemoryRateLimiter : IRateLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, ResourceHits> _resources = new();

    public InMemoryRateLimiter(int limitPerWindow = 100, TimeSpan? window = null)
    {
        _limit = limitPerWindow < 1 ? 1 : limitPerWindow;
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public Task<bool> TryAcquireAsync(string resource, CancellationToken ct)
    {
        var key = string.IsNullOrEmpty(resource) ? "_" : resource;
        var state = _resources.GetOrAdd(key, _ => new ResourceHits());
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - _window;

        lock (state.Sync)
        {
            Prune(state.Hits, cutoff);
            if (state.Hits.Count >= _limit)
                return Task.FromResult(false);

            state.Hits.Add(now);
            return Task.FromResult(true);
        }
    }

    public Task<RateLimitInfo> GetInfoAsync(string resource, CancellationToken ct)
    {
        var key = string.IsNullOrEmpty(resource) ? "_" : resource;
        if (!_resources.TryGetValue(key, out var state))
            return Task.FromResult(new RateLimitInfo(_limit, _limit, DateTimeOffset.UtcNow.Add(_window)));

        var now = DateTimeOffset.UtcNow;
        var cutoff = now - _window;

        lock (state.Sync)
        {
            Prune(state.Hits, cutoff);
            var used = state.Hits.Count;
            var remaining = Math.Max(0, _limit - used);
            var resetsAt = state.Hits.Count > 0
                ? state.Hits[0].Add(_window)
                : now.Add(_window);
            return Task.FromResult(new RateLimitInfo(remaining, _limit, resetsAt));
        }
    }

    private static void Prune(List<DateTimeOffset> hits, DateTimeOffset cutoff)
    {
        var i = 0;
        while (i < hits.Count && hits[i] < cutoff)
            i++;
        if (i > 0)
            hits.RemoveRange(0, i);
    }

    private sealed class ResourceHits
    {
        public object Sync { get; } = new();
        public List<DateTimeOffset> Hits { get; } = new();
    }
}
