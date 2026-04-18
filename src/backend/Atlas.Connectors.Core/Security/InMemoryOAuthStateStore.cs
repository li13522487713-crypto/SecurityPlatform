using System.Collections.Concurrent;

namespace Atlas.Connectors.Core.Security;

/// <summary>
/// 进程内 OAuth state 存储。多实例 host 应替换为基于 HybridCache / Redis 的实现。
/// </summary>
public sealed class InMemoryOAuthStateStore : IOAuthStateStore
{
    private readonly ConcurrentDictionary<string, OAuthState> _store = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryOAuthStateStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task SaveAsync(OAuthState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        _store[state.Value] = state;
        return Task.CompletedTask;
    }

    public Task<OAuthState?> ConsumeAsync(string stateValue, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_store.TryRemove(stateValue, out var state))
        {
            return Task.FromResult<OAuthState?>(null);
        }
        if (state.IsExpired(_timeProvider.GetUtcNow()))
        {
            return Task.FromResult<OAuthState?>(null);
        }
        return Task.FromResult<OAuthState?>(state);
    }
}
