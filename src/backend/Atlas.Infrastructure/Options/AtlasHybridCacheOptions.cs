namespace Atlas.Infrastructure.Options;

public sealed class AtlasHybridCacheOptions
{
    public AtlasHybridCacheRedisOptions Redis { get; init; } = new();

    /// <summary>
    /// 当分布式缓存不可用时，是否自动回退到仅本地缓存模式。
    /// </summary>
    public bool FallbackToLocalWhenRedisUnavailable { get; init; } = true;
}

public sealed class AtlasHybridCacheRedisOptions
{
    public bool Enabled { get; init; }

    public string Configuration { get; init; } = string.Empty;

    public string InstanceName { get; init; } = "atlas:";
}

