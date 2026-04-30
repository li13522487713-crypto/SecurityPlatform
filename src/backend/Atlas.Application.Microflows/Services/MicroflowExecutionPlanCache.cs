using System.Collections.Concurrent;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowExecutionPlanCache : IMicroflowExecutionPlanCache
{
    private readonly ConcurrentDictionary<string, Lazy<Task<MicroflowExecutionPlan>>> _entries = new(StringComparer.Ordinal);

    public async ValueTask<MicroflowExecutionPlan> GetOrCreateAsync(
        MicroflowExecutionPlanCacheKey key,
        Func<CancellationToken, Task<MicroflowExecutionPlan>> factory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = _entries.GetOrAdd(
            key.StableKey,
            _ => new Lazy<Task<MicroflowExecutionPlan>>(
                () => factory(cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await entry.Value.ConfigureAwait(false);
        }
        catch
        {
            _entries.TryRemove(key.StableKey, out _);
            throw;
        }
    }

    public void Invalidate(string resourceId, string? version)
    {
        foreach (var key in _entries.Keys)
        {
            if (!key.Contains(resourceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(version) && !key.Contains(version, StringComparison.Ordinal))
            {
                continue;
            }

            _entries.TryRemove(key, out _);
        }
    }
}
