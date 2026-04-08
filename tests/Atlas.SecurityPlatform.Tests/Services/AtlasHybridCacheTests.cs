using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class AtlasHybridCacheTests
{
    [Fact]
    public async Task SetAndTryGetAsync_ShouldHit()
    {
        using var serviceProvider = CreateServiceProvider();
        var cache = serviceProvider.GetRequiredService<IAtlasHybridCache>();

        await cache.SetAsync("test:key:hit", "value-1", TimeSpan.FromMinutes(5));
        var result = await cache.TryGetAsync<string>("test:key:hit");

        Assert.True(result.Found);
        Assert.Equal("value-1", result.Value);
    }

    [Fact]
    public async Task TryGetAsync_WhenMiss_ShouldNotPolluteCache()
    {
        using var serviceProvider = CreateServiceProvider();
        var cache = serviceProvider.GetRequiredService<IAtlasHybridCache>();

        var miss = await cache.TryGetAsync<string>("test:key:miss");
        Assert.False(miss.Found);

        var factoryCount = 0;
        var value = await cache.GetOrCreateAsync(
            "test:key:miss",
            _ =>
            {
                factoryCount++;
                return new ValueTask<string?>("from-factory");
            },
            TimeSpan.FromMinutes(5));

        Assert.Equal("from-factory", value);
        Assert.Equal(1, factoryCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldCacheNullValue()
    {
        using var serviceProvider = CreateServiceProvider();
        var cache = serviceProvider.GetRequiredService<IAtlasHybridCache>();

        var factoryCount = 0;
        var key = "test:key:null";

        var first = await cache.GetOrCreateAsync<string?>(
            key,
            _ =>
            {
                factoryCount++;
                return new ValueTask<string?>((string?)null);
            },
            TimeSpan.FromMinutes(5));

        var second = await cache.GetOrCreateAsync<string?>(
            key,
            _ =>
            {
                factoryCount++;
                return new ValueTask<string?>("unexpected");
            },
            TimeSpan.FromMinutes(5));

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, factoryCount);
    }

    [Fact]
    public async Task RemoveByTagAsync_ShouldInvalidateRelatedKeys()
    {
        using var serviceProvider = CreateServiceProvider();
        var cache = serviceProvider.GetRequiredService<IAtlasHybridCache>();

        var tag = "tag:test:group";
        await cache.SetAsync("test:key:tag:1", "a", TimeSpan.FromMinutes(5), [tag]);
        await cache.SetAsync("test:key:tag:2", "b", TimeSpan.FromMinutes(5), [tag]);

        await cache.RemoveByTagAsync(tag);

        var v1 = await cache.TryGetAsync<string>("test:key:tag:1");
        var v2 = await cache.TryGetAsync<string>("test:key:tag:2");

        Assert.False(v1.Found);
        Assert.False(v2.Found);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<IOptions<AtlasHybridCacheOptions>>(
            Options.Create(new AtlasHybridCacheOptions
            {
                FallbackToLocalWhenRedisUnavailable = true
            }));
        services.AddSingleton<IAtlasHybridCache, AtlasHybridCache>();
        return services.BuildServiceProvider();
    }
}

