using System.Collections.Concurrent;

namespace Atlas.Connectors.Core.Security;

/// <summary>
/// 重放窗口防护：拒绝时间戳过老或重复的 IdempotencyKey。
/// 默认基于 ConcurrentDictionary + TTL 的进程内实现；分布式部署应替换为 Redis 实现。
/// </summary>
public interface IReplayGuard
{
    /// <summary>
    /// 检查并记录。若 key 已存在或时间戳已超出窗口，返回 false。
    /// </summary>
    bool TryAccept(string idempotencyKey, DateTimeOffset eventTime, TimeSpan window);
}

public sealed class InMemoryReplayGuard : IReplayGuard
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryReplayGuard(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAccept(string idempotencyKey, DateTimeOffset eventTime, TimeSpan window)
    {
        var now = _timeProvider.GetUtcNow();
        if (now - eventTime > window)
        {
            return false;
        }

        // 顺手清理过期项，避免无界增长（小开销，window 通常 5 分钟内）。
        if (_seen.Count > 1024)
        {
            foreach (var (key, ts) in _seen)
            {
                if (now - ts > window)
                {
                    _seen.TryRemove(key, out _);
                }
            }
        }

        return _seen.TryAdd(idempotencyKey, now);
    }
}
