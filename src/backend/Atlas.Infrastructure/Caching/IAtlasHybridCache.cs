namespace Atlas.Infrastructure.Caching;

public interface IAtlasHybridCache
{
    ValueTask<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T?>> valueFactory,
        TimeSpan expiration,
        IEnumerable<string>? tags = null,
        bool localOnly = false,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync<T>(
        string key,
        T? value,
        TimeSpan expiration,
        IEnumerable<string>? tags = null,
        bool localOnly = false,
        CancellationToken cancellationToken = default);

    ValueTask<AtlasCacheLookupResult<T>> TryGetAsync<T>(
        string key,
        bool localOnly = false,
        CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);

    ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}

public readonly record struct AtlasCacheLookupResult<T>(bool Found, T? Value)
{
    public static AtlasCacheLookupResult<T> Miss => new(false, default);

    public static AtlasCacheLookupResult<T> Hit(T? value) => new(true, value);
}

