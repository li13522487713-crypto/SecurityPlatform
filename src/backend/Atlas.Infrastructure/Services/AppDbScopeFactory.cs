using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;
using System.Collections.Concurrent;
using Atlas.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

public sealed class AppDbScopeFactory : IAppDbScopeFactory, IDisposable
{
    private const string AppClientCachePrefix = "app-db-client:";
    private const int AppClientCacheLimit = 128;
    private static readonly TimeSpan AppClientSlidingExpiration = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan AppClientAbsoluteExpiration = TimeSpan.FromHours(2);

    private readonly ITenantDbConnectionFactory _connectionFactory;
    private readonly ISqlSugarClient _mainDb;
    private readonly IAtlasHybridCache _cache;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<AppDbScopeFactory> _logger;
    private readonly MemoryCache _appClientCache;

    // 缓存已完成 Schema 初始化的 (tenantId:appInstanceId) 组合，进程生命周期内有效
    private static readonly ConcurrentDictionary<string, bool> _schemaInitializedKeys
        = new(StringComparer.OrdinalIgnoreCase);

    // 按 (tenantId:appId) 缓存 SqlSugarScope（线程安全单例），避免并发请求对同一 SQLite 文件各自建连接导致写锁冲突
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _clientCacheLocks
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan RoutePolicyCacheDuration = TimeSpan.FromMinutes(5);

    public AppDbScopeFactory(
        ITenantDbConnectionFactory connectionFactory,
        ISqlSugarClient mainDb,
        IAtlasHybridCache cache,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<AppDbScopeFactory> logger)
    {
        _connectionFactory = connectionFactory;
        _mainDb = mainDb;
        _cache = cache;
        _idGeneratorAccessor = idGeneratorAccessor;
        _logger = logger;
        _appClientCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = AppClientCacheLimit
        });
    }

    public async Task<ISqlSugarClient> GetAppClientAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.AppContextRequired, "应用上下文缺失，无法解析应用级数据库连接。");
        }

        // 缓存路由策略，避免每次查主库
        var policyCacheKey = AtlasCacheKeys.RoutePolicy.AppDataRoute(tenantId, appInstanceId);
        var isMainOnly = await _cache.GetOrCreateAsync(
            policyCacheKey,
            async ct =>
            {
                var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
                    .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId, ct);
                return policy is not null && string.Equals(policy.Mode, "MainOnly", StringComparison.OrdinalIgnoreCase);
            },
            RoutePolicyCacheDuration,
            [AtlasCacheTags.RoutePolicy(tenantId, appInstanceId)],
            cancellationToken: cancellationToken);

        if (isMainOnly)
        {
            return _mainDb;
        }

        var tenantIdValue = tenantId.Value.ToString();
        var info = await _connectionFactory.GetConnectionInfoAsync(tenantIdValue, appInstanceId, cancellationToken);
        if (info is null)
        {
            var recoveredByLegacy = await TryRepairBindingFromLegacyDataSourceAsync(
                tenantId,
                appInstanceId,
                cancellationToken);
            if (recoveredByLegacy)
            {
                info = await _connectionFactory.GetConnectionInfoAsync(tenantIdValue, appInstanceId, cancellationToken);
            }
        }

        if (info is null)
        {
            await EnsureMainOnlyRoutePolicyAsync(tenantId, appInstanceId, cancellationToken);
            _logger.LogWarning(
                "应用数据源未绑定，自动降级为 MainOnly。TenantId={TenantId}; AppInstanceId={AppInstanceId}",
                tenantId.Value,
                appInstanceId);
            return _mainDb;
        }

        var dbType = MapDbType(info.DbType);
        var schemaKey = $"{tenantId.Value}:{appInstanceId}";
        var clientCacheKey = $"{AppClientCachePrefix}{schemaKey}";

        if (_appClientCache.TryGetValue(clientCacheKey, out ISqlSugarClient? cachedClient) && cachedClient is not null)
        {
            return cachedClient;
        }

        var cacheLock = _clientCacheLocks.GetOrAdd(schemaKey, _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);
        ISqlSugarClient db;
        try
        {
            if (_appClientCache.TryGetValue(clientCacheKey, out cachedClient) && cachedClient is not null)
            {
                db = cachedClient;
            }
            else
            {
                var config = new ConnectionConfig
                {
                    ConnectionString = info.ConnectionString,
                    DbType = dbType,
                    IsAutoCloseConnection = true,
                    ConfigureExternalServices = new ConfigureExternalServices
                    {
                        EntityService = (property, column) =>
                        {
                            if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
                                && property.PropertyType == typeof(Atlas.Core.Tenancy.TenantId))
                            {
                                column.IsIgnore = true;
                            }
                        }
                    }
                };

                var scope = new SqlSugarScope(config);
                scope.QueryFilter.AddTableFilter<Atlas.Core.Abstractions.TenantEntity>(it => it.TenantIdValue == tenantId.Value);
                db = scope;
                _appClientCache.Set(
                    clientCacheKey,
                    db,
                    new MemoryCacheEntryOptions
                    {
                        Size = 1,
                        SlidingExpiration = AppClientSlidingExpiration,
                        AbsoluteExpirationRelativeToNow = AppClientAbsoluteExpiration
                    }.RegisterPostEvictionCallback(static (key, value, reason, state) =>
                    {
                        if (key is not string cacheKey || !cacheKey.StartsWith(AppClientCachePrefix, StringComparison.Ordinal))
                        {
                            return;
                        }

                        var schemaKey = cacheKey[AppClientCachePrefix.Length..];
                        if (_clientCacheLocks.TryRemove(schemaKey, out var removedLock))
                        {
                            removedLock.Dispose();
                        }

                        if (value is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }));
            }
        }
        finally
        {
            cacheLock.Release();
        }

        // 只在进程首次访问该应用库时执行 Schema 检查，后续跳过
        if (!_schemaInitializedKeys.ContainsKey(schemaKey))
        {
            EnsureAppSchema(db);
            _schemaInitializedKeys.TryAdd(schemaKey, true);
        }

        return db;
    }

    public async Task<ISqlSugarClient?> TryGetAppClientAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAppClientAsync(tenantId, appInstanceId, cancellationToken);
        }
        catch (BusinessException ex) when (
            string.Equals(ex.Code, ErrorCodes.AppDataSourceNotBound, StringComparison.Ordinal)
            || string.Equals(ex.Code, ErrorCodes.AppContextRequired, StringComparison.Ordinal))
        {
            return null;
        }
    }

    public void InvalidateAppClientCache(TenantId tenantId, long appInstanceId)
    {
        if (appInstanceId <= 0)
        {
            return;
        }

        var schemaKey = $"{tenantId.Value}:{appInstanceId}";
        var clientCacheKey = $"{AppClientCachePrefix}{schemaKey}";
        _appClientCache.Remove(clientCacheKey);
        _schemaInitializedKeys.TryRemove(schemaKey, out _);

        var policyCacheKey = AtlasCacheKeys.RoutePolicy.AppDataRoute(tenantId, appInstanceId);
        HybridCacheSyncBridge.Run(_cache.RemoveAsync(policyCacheKey));
    }

    public void Dispose()
    {
        _appClientCache.Dispose();
    }

    private static void EnsureAppSchema(ISqlSugarClient db)
    {
        // 应用数据面首次切换到新库（或缓存命中空库）时，按统一 ORM 目录兜底创建运行时表，避免 no such table 异常。
        // 以 AppRole 作为 schema 就绪检测目标（应用级基础身份表，必定在任何租户实例中存在）。
        if (!db.DbMaintenance.IsAnyTable("AppRole", false))
        {
            AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db);
        }
    }

    private static DbType MapDbType(string? dbType)
    {
        return DataSourceDriverRegistry.ResolveDbType(dbType);
    }

    private async Task<bool> TryRepairBindingFromLegacyDataSourceAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        // LowCodeApp 已移除：不再从旧表修复数据源绑定。
        _ = tenantId;
        _ = appInstanceId;
        await Task.CompletedTask;
        return false;
    }

    private async Task EnsureMainOnlyRoutePolicyAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId, cancellationToken);
        if (policy is null)
        {
            var entity = new AppDataRoutePolicy(
                tenantId,
                appInstanceId,
                "MainOnly",
                readOnlyWindow: false,
                dualWriteEnabled: false,
                updatedBy: 0,
                id: _idGeneratorAccessor.NextId(),
                now);
            await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            policy.SetMode("MainOnly", readOnlyWindow: false, dualWriteEnabled: false, updatedBy: 0, now);
            await _mainDb.Updateable(policy)
                .Where(x => x.Id == policy.Id && x.TenantIdValue == tenantId.Value)
                .ExecuteCommandAsync(cancellationToken);
        }

        _connectionFactory.InvalidateCache(tenantId.Value.ToString("D"), appInstanceId);
        var policyCacheKey = AtlasCacheKeys.RoutePolicy.AppDataRoute(tenantId, appInstanceId);
        await _cache.RemoveAsync(policyCacheKey);
    }
}
