using System.Collections.Concurrent;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowExecutionPlanCache : IMicroflowExecutionPlanCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    public async ValueTask<MicroflowExecutionPlan> GetOrCreateAsync(
        MicroflowExecutionPlanCacheKey key,
        Func<CancellationToken, Task<MicroflowExecutionPlan>> factory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = _entries.GetOrAdd(
            key.StableKey,
            _ => new CacheEntry(
                key,
                new Lazy<Task<MicroflowExecutionPlan>>(
                    () => factory(cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication)));

        try
        {
            return await entry.Plan.Value.ConfigureAwait(false);
        }
        catch
        {
            _entries.TryRemove(key.StableKey, out _);
            throw;
        }
    }

    public void Invalidate(string resourceId, string? version)
    {
        foreach (var (stableKey, entry) in _entries)
        {
            if (!string.Equals(entry.Key.ResourceId, resourceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(version) && !string.Equals(entry.Key.Version, version, StringComparison.Ordinal))
            {
                continue;
            }

            _entries.TryRemove(stableKey, out _);
        }
    }

    private sealed record CacheEntry(
        MicroflowExecutionPlanCacheKey Key,
        Lazy<Task<MicroflowExecutionPlan>> Plan);
}
