using System.Collections.Concurrent;

namespace Atlas.Application.Microflows.Runtime;

/// <summary>
/// 跨请求维护「正在跑的 microflow 运行」的 cancellation handle。P0-6 引入：
///
/// - 旧实现 <c>MicroflowTestRunService.CancelAsync</c> 只更新 DB session 状态，
///   并不能让正在执行的引擎主循环停下；本注册表使后续 cancel API 调用能向
///   引擎传递 <see cref="CancellationToken"/>。
/// - 任何 runId 在引擎开跑前调用 <see cref="Register"/> 拿到一个 token，
///   引擎主循环里持续 <c>ThrowIfCancellationRequested</c>。
/// - <see cref="Cancel"/> 标记目标 run 为取消并让对应 token 取消；不存在时返回 false。
/// - 引擎完成后必须 <see cref="Unregister"/>，避免内存泄漏。
///
/// Scope：DI 注册为单例，进程内共享；多实例部署需后续接入分布式实现（P2）。
/// </summary>
public interface IMicroflowRunCancellationRegistry
{
    /// <summary>注册并返回一个与外部 token 联动的 <see cref="CancellationTokenSource"/>。</summary>
    CancellationTokenSource Register(string runId, CancellationToken externalToken);

    /// <summary>登记 run 已结束（成功/失败/取消），释放对应 cts。</summary>
    void Unregister(string runId);

    /// <summary>请求取消指定 run；若该 run 已结束或未登记返回 false。</summary>
    bool Cancel(string runId);

    /// <summary>判断 run 是否仍在登记中（仅诊断用）。</summary>
    bool IsRegistered(string runId);
}

public sealed class MicroflowRunCancellationRegistry : IMicroflowRunCancellationRegistry, IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);

    public CancellationTokenSource Register(string runId, CancellationToken externalToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        // 同一 runId 罕见，但若先前未 Unregister 应该清掉旧实例，避免泄漏。
        if (_runs.TryRemove(runId, out var stale))
        {
            try { stale.Dispose(); } catch { /* swallow disposal race */ }
        }
        _runs[runId] = linked;
        return linked;
    }

    public void Unregister(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return;
        }

        if (_runs.TryRemove(runId, out var cts))
        {
            try { cts.Dispose(); } catch { /* swallow disposal race */ }
        }
    }

    public bool Cancel(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return false;
        }

        if (!_runs.TryGetValue(runId, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public bool IsRegistered(string runId)
        => !string.IsNullOrWhiteSpace(runId) && _runs.ContainsKey(runId);

    public void Dispose()
    {
        foreach (var cts in _runs.Values)
        {
            try { cts.Dispose(); } catch { /* swallow */ }
        }
        _runs.Clear();
    }
}
