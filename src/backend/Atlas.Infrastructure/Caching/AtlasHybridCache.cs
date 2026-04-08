using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Atlas.Infrastructure.Caching;

public sealed class AtlasHybridCache : IAtlasHybridCache
{
    private static readonly TimeSpan LocalFallbackEvictionDelay = TimeSpan.FromMilliseconds(1);

    private readonly HybridCache _hybridCache;
    private readonly AtlasHybridCacheOptions _options;

    // 本地标签索引用于 Redis 不可用回退时，支持按 Tag 近似失效（仅当前进程）。
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagKeyIndex =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _keyTagIndex =
        new(StringComparer.Ordinal);

    public AtlasHybridCache(
        HybridCache hybridCache,
        IOptions<AtlasHybridCacheOptions> options)
    {
        _hybridCache = hybridCache;
        _options = options.Value;
    }

    public async ValueTask<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T?>> valueFactory,
        TimeSpan expiration,
        IEnumerable<string>? tags = null,
        bool localOnly = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedTags = NormalizeTags(tags);
        var cacheOptions = BuildEntryOptions(expiration, localOnly, disableUnderlyingData: false);

        try
        {
            var envelope = await _hybridCache.GetOrCreateAsync(
                key,
                async token => AtlasCacheEnvelope<T>.FromValue(await valueFactory(token)),
                cacheOptions,
                normalizedTags,
                cancellationToken);
            TrackTags(key, normalizedTags);
            return envelope.Value;
        }
        catch when (_options.FallbackToLocalWhenRedisUnavailable && !localOnly)
        {
            var localOnlyOptions = BuildEntryOptions(expiration, localOnly: true, disableUnderlyingData: false);
            var envelope = await _hybridCache.GetOrCreateAsync(
                key,
                async token => AtlasCacheEnvelope<T>.FromValue(await valueFactory(token)),
                localOnlyOptions,
                normalizedTags,
                cancellationToken);
            TrackTags(key, normalizedTags);
            return envelope.Value;
        }
    }

    public async ValueTask SetAsync<T>(
        string key,
        T? value,
        TimeSpan expiration,
        IEnumerable<string>? tags = null,
        bool localOnly = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedTags = NormalizeTags(tags);
        var cacheOptions = BuildEntryOptions(expiration, localOnly, disableUnderlyingData: false);

        try
        {
            await _hybridCache.SetAsync(
                key,
                AtlasCacheEnvelope<T>.FromValue(value),
                cacheOptions,
                normalizedTags,
                cancellationToken);
            TrackTags(key, normalizedTags);
        }
        catch when (_options.FallbackToLocalWhenRedisUnavailable && !localOnly)
        {
            var localOnlyOptions = BuildEntryOptions(expiration, localOnly: true, disableUnderlyingData: false);
            await _hybridCache.SetAsync(
                key,
                AtlasCacheEnvelope<T>.FromValue(value),
                localOnlyOptions,
                normalizedTags,
                cancellationToken);
            TrackTags(key, normalizedTags);
        }
    }

    public async ValueTask<AtlasCacheLookupResult<T>> TryGetAsync<T>(
        string key,
        bool localOnly = false,
        CancellationToken cancellationToken = default)
    {
        var options = BuildEntryOptions(
            expiration: TimeSpan.FromMinutes(5),
            localOnly: localOnly,
            disableUnderlyingData: true);
        try
        {
            var envelope = await _hybridCache.GetOrCreateAsync<AtlasCacheEnvelope<T>?>(
                key,
                static _ => default,
                options,
                tags: null,
                cancellationToken);

            if (envelope is null)
            {
                return AtlasCacheLookupResult<T>.Miss;
            }

            return AtlasCacheLookupResult<T>.Hit(envelope.Value);
        }
        catch when (_options.FallbackToLocalWhenRedisUnavailable && !localOnly)
        {
            var localOptions = BuildEntryOptions(
                expiration: TimeSpan.FromMinutes(5),
                localOnly: true,
                disableUnderlyingData: true);
            var envelope = await _hybridCache.GetOrCreateAsync<AtlasCacheEnvelope<T>?>(
                key,
                static _ => default,
                localOptions,
                tags: null,
                cancellationToken);

            if (envelope is null)
            {
                return AtlasCacheLookupResult<T>.Miss;
            }

            return AtlasCacheLookupResult<T>.Hit(envelope.Value);
        }
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hybridCache.RemoveAsync(key, cancellationToken);
        }
        catch when (_options.FallbackToLocalWhenRedisUnavailable)
        {
            await ForceLocalEvictAsync(key, cancellationToken);
        }
        finally
        {
            UntrackKey(key);
        }
    }

    public async ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        try
        {
            await _hybridCache.RemoveByTagAsync(tag, cancellationToken);
            UntrackTag(tag);
        }
        catch when (_options.FallbackToLocalWhenRedisUnavailable)
        {
            await RemoveByTagLocalFallbackAsync(tag, cancellationToken);
        }
    }

    public async ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var normalizedTags = NormalizeTags(tags);
        if (normalizedTags.Length == 0)
        {
            return;
        }

        try
        {
            await _hybridCache.RemoveByTagAsync(normalizedTags, cancellationToken);
            foreach (var tag in normalizedTags)
            {
                UntrackTag(tag);
            }
        }
        catch when (_options.FallbackToLocalWhenRedisUnavailable)
        {
            foreach (var tag in normalizedTags)
            {
                await RemoveByTagLocalFallbackAsync(tag, cancellationToken);
            }
        }
    }

    private async ValueTask RemoveByTagLocalFallbackAsync(string tag, CancellationToken cancellationToken)
    {
        if (!_tagKeyIndex.TryGetValue(tag, out var keys))
        {
            return;
        }

        foreach (var key in keys.Keys)
        {
            await ForceLocalEvictAsync(key, cancellationToken);
            UntrackKey(key);
        }

        UntrackTag(tag);
    }

    private async ValueTask ForceLocalEvictAsync(string key, CancellationToken cancellationToken)
    {
        await _hybridCache.SetAsync(
            key,
            AtlasCacheEnvelope<object?>.FromValue(null),
            BuildEntryOptions(LocalFallbackEvictionDelay, localOnly: true, disableUnderlyingData: false),
            tags: null,
            cancellationToken);
    }

    private void TrackTags(string key, string[] tags)
    {
        if (tags.Length == 0)
        {
            return;
        }

        var keyTags = _keyTagIndex.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        foreach (var tag in tags)
        {
            keyTags.TryAdd(tag, 0);
            var keys = _tagKeyIndex.GetOrAdd(tag, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            keys.TryAdd(key, 0);
        }
    }

    private void UntrackKey(string key)
    {
        if (!_keyTagIndex.TryRemove(key, out var tags))
        {
            return;
        }

        foreach (var tag in tags.Keys)
        {
            if (!_tagKeyIndex.TryGetValue(tag, out var keys))
            {
                continue;
            }

            keys.TryRemove(key, out _);
            if (keys.IsEmpty)
            {
                _tagKeyIndex.TryRemove(tag, out _);
            }
        }
    }

    private void UntrackTag(string tag)
    {
        if (!_tagKeyIndex.TryRemove(tag, out var keys))
        {
            return;
        }

        foreach (var key in keys.Keys)
        {
            if (!_keyTagIndex.TryGetValue(key, out var tags))
            {
                continue;
            }

            tags.TryRemove(tag, out _);
            if (tags.IsEmpty)
            {
                _keyTagIndex.TryRemove(key, out _);
            }
        }
    }

    private static string[] NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return tags
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static HybridCacheEntryOptions BuildEntryOptions(
        TimeSpan expiration,
        bool localOnly,
        bool disableUnderlyingData)
    {
        HybridCacheEntryFlags flags = HybridCacheEntryFlags.None;
        if (localOnly)
        {
            flags |= HybridCacheEntryFlags.DisableDistributedCache;
        }

        if (disableUnderlyingData)
        {
            flags |= HybridCacheEntryFlags.DisableUnderlyingData;
        }

        return new HybridCacheEntryOptions
        {
            Expiration = expiration,
            LocalCacheExpiration = expiration,
            Flags = flags
        };
    }

    private sealed record AtlasCacheEnvelope<T>
    {
        public T? Value { get; init; }

        public static AtlasCacheEnvelope<T> FromValue(T? value) => new() { Value = value };
    }
}
