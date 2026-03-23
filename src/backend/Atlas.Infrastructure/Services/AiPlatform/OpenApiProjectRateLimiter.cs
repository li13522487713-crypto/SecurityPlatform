using System.Collections.Concurrent;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OpenApiProjectRateLimiter
{
    private readonly ConcurrentDictionary<string, CounterState> _counters = new(StringComparer.Ordinal);
    private readonly int _limitPerMinute;

    public OpenApiProjectRateLimiter(IOptions<AiPlatformOptions> aiOptions)
    {
        _limitPerMinute = Math.Max(1, aiOptions.Value.OpenApiGovernance.ProjectRateLimitPerMinute);
    }

    public bool TryAcquire(string key, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        var now = DateTime.UtcNow;
        var window = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        var state = _counters.GetOrAdd(key, _ => new CounterState(window, 0));

        lock (state.SyncRoot)
        {
            if (state.WindowStartUtc != window)
            {
                state.WindowStartUtc = window;
                state.Count = 0;
            }

            if (state.Count >= _limitPerMinute)
            {
                retryAfterSeconds = Math.Max(1, 60 - now.Second);
                return false;
            }

            state.Count += 1;
            retryAfterSeconds = 0;
            return true;
        }
    }

    private sealed class CounterState
    {
        public CounterState(DateTime windowStartUtc, int count)
        {
            WindowStartUtc = windowStartUtc;
            Count = count;
        }

        public object SyncRoot { get; } = new();

        public DateTime WindowStartUtc { get; set; }

        public int Count { get; set; }
    }
}
