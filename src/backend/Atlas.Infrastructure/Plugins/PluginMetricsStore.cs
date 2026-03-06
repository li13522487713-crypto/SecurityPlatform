using System.Collections.Concurrent;

namespace Atlas.Infrastructure.Plugins;

/// <summary>
/// 插件运行时指标（内存计数器，进程生命周期内累积）
/// </summary>
public sealed class PluginMetricsStore
{
    private readonly ConcurrentDictionary<string, PluginMetricsEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void RecordCall(string pluginCode, bool success, long elapsedMs)
    {
        var entry = _entries.GetOrAdd(pluginCode, _ => new PluginMetricsEntry(pluginCode));
        entry.RecordCall(success, elapsedMs);
    }

    public PluginMetricsSnapshot? GetSnapshot(string pluginCode)
    {
        if (!_entries.TryGetValue(pluginCode, out var entry)) return null;
        return entry.GetSnapshot();
    }

    public IReadOnlyList<PluginMetricsSnapshot> GetAllSnapshots()
    {
        return _entries.Values.Select(e => e.GetSnapshot()).ToList();
    }
}

internal sealed class PluginMetricsEntry
{
    private readonly string _code;
    private long _totalCalls;
    private long _errorCalls;
    private long _totalElapsedMs;

    // 用 int 表示 bool（0=关闭, 1=打开），通过 Interlocked.CompareExchange 实现无锁原子翻转，
    // 避免普通 bool 字段在多线程下的可见性与 TOCTOU 问题。
    private int _circuitOpen;

    // 存储 DateTimeOffset.UtcNow.Ticks（long），通过 Interlocked 读写保证内存可见性。
    // 0 表示熔断器从未开启。
    private long _circuitOpenedAtTicks;

    private const int CircuitBreakerThreshold = 5;
    private static readonly TimeSpan CircuitResetTimeout = TimeSpan.FromMinutes(2);

    public PluginMetricsEntry(string code) => _code = code;

    public void RecordCall(bool success, long elapsedMs)
    {
        Interlocked.Increment(ref _totalCalls);
        Interlocked.Add(ref _totalElapsedMs, elapsedMs);
        if (!success)
        {
            Interlocked.Increment(ref _errorCalls);
            CheckCircuitBreaker();
        }
        else
        {
            TryResetCircuitBreaker();
        }
    }

    private void CheckCircuitBreaker()
    {
        // 已开启则无需重复触发
        if (Volatile.Read(ref _circuitOpen) == 1) return;

        var errors = Interlocked.Read(ref _errorCalls);
        var total = Interlocked.Read(ref _totalCalls);
        var errorRate = total == 0 ? 0 : (double)errors / total;

        if (errors >= CircuitBreakerThreshold && errorRate > 0.5)
        {
            // CAS：只有从 0→1 成功的线程才负责记录开启时间，防止多线程重复写入
            if (Interlocked.CompareExchange(ref _circuitOpen, 1, 0) == 0)
            {
                Interlocked.Exchange(ref _circuitOpenedAtTicks, DateTimeOffset.UtcNow.Ticks);
            }
        }
    }

    private void TryResetCircuitBreaker()
    {
        if (Volatile.Read(ref _circuitOpen) == 0) return;

        var openedTicks = Interlocked.Read(ref _circuitOpenedAtTicks);
        if (openedTicks == 0) return;

        var elapsed = DateTimeOffset.UtcNow - new DateTimeOffset(openedTicks, TimeSpan.Zero);
        if (elapsed > CircuitResetTimeout)
        {
            // CAS：只有从 1→0 成功的线程才清零开启时间，保证写操作的原子性
            if (Interlocked.CompareExchange(ref _circuitOpen, 0, 1) == 1)
            {
                Interlocked.Exchange(ref _circuitOpenedAtTicks, 0);
            }
        }
    }

    public PluginMetricsSnapshot GetSnapshot()
    {
        var total = Interlocked.Read(ref _totalCalls);
        var errors = Interlocked.Read(ref _errorCalls);
        var elapsed = Interlocked.Read(ref _totalElapsedMs);
        var isOpen = Volatile.Read(ref _circuitOpen) == 1;
        var openedTicks = Interlocked.Read(ref _circuitOpenedAtTicks);
        DateTimeOffset? openedAt = isOpen && openedTicks != 0
            ? new DateTimeOffset(openedTicks, TimeSpan.Zero)
            : null;

        return new PluginMetricsSnapshot(
            _code,
            (int)total,
            (int)errors,
            total == 0 ? 0 : (double)errors / total,
            total == 0 ? 0 : elapsed / total,
            isOpen,
            openedAt);
    }
}

public sealed record PluginMetricsSnapshot(
    string PluginCode,
    int TotalCalls,
    int ErrorCalls,
    double ErrorRate,
    long AvgElapsedMs,
    bool CircuitOpen,
    DateTimeOffset? CircuitOpenedAt);
