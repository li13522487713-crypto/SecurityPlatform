using System.Collections.Concurrent;
using Atlas.Core.Observability;

namespace Atlas.Infrastructure.Observability;

public sealed class InMemoryNodeMetricsCollector : INodeMetricsCollector
{
    private sealed class MutableState
    {
        internal long TotalExecutions;
        internal long SuccessCount;
        internal long FailureCount;
        internal long SumDurationMs;
        internal long MaxDurationMs;
        internal long MinDurationMs = long.MaxValue;
        internal DateTime LastExecutedAt;
        internal readonly object Gate = new();
    }

    private readonly ConcurrentDictionary<string, MutableState> _byKey = new(StringComparer.Ordinal);

    public void RecordExecution(string nodeTypeKey, long durationMs, bool succeeded)
    {
        var key = string.IsNullOrWhiteSpace(nodeTypeKey) ? "_" : nodeTypeKey;
        var state = _byKey.GetOrAdd(key, static _ => new MutableState());
        lock (state.Gate)
        {
            state.TotalExecutions++;
            if (succeeded)
                state.SuccessCount++;
            else
                state.FailureCount++;
            state.SumDurationMs += durationMs;
            if (durationMs > state.MaxDurationMs)
                state.MaxDurationMs = durationMs;
            if (durationMs < state.MinDurationMs)
                state.MinDurationMs = durationMs;
            state.LastExecutedAt = DateTime.UtcNow;
        }
    }

    public NodeMetrics GetMetrics(string nodeTypeKey)
    {
        var key = string.IsNullOrWhiteSpace(nodeTypeKey) ? "_" : nodeTypeKey;
        if (!_byKey.TryGetValue(key, out var state))
            return new NodeMetrics(key, 0, 0, 0, 0, 0, 0, default);

        lock (state.Gate)
        {
            if (state.TotalExecutions == 0)
                return new NodeMetrics(key, 0, 0, 0, 0, 0, 0, default);
            var avg = state.SumDurationMs / (double)state.TotalExecutions;
            var min = state.MinDurationMs == long.MaxValue ? 0L : state.MinDurationMs;
            return new NodeMetrics(
                key,
                state.TotalExecutions,
                state.SuccessCount,
                state.FailureCount,
                avg,
                state.MaxDurationMs,
                min,
                state.LastExecutedAt);
        }
    }

    public IReadOnlyDictionary<string, NodeMetrics> GetAllMetrics()
    {
        var dict = new Dictionary<string, NodeMetrics>(StringComparer.Ordinal);
        foreach (var kv in _byKey)
            dict[kv.Key] = GetMetrics(kv.Key);
        return dict;
    }
}
