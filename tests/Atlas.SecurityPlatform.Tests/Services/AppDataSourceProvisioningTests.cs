using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class AppDataSourceProvisioningTests
{
    [Fact]
    public async Task AppDataSourceProvisioner_ShouldCreateDataSourceBindingAndPolicy()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            db.CodeFirst.InitTables(typeof(TenantDataSource), typeof(TenantAppDataSourceBinding), typeof(AppDataRoutePolicy));
            var tenantId = new TenantId(Guid.Parse("61616161-6161-6161-6161-616161616161"));
            var appDbScopeFactory = new FakeAppDbScopeFactory(db);
            var provisioningService = new AppDatabaseProvisioningService(appDbScopeFactory);
            var provisioner = new AppDataSourceProvisioner(
                db,
                new SequentialIdGenerator(1000),
                new NoopTenantDbConnectionFactory(),
                provisioningService,
                Options.Create(new DatabaseEncryptionOptions { Enabled = false, Key = "test-key" }),
                NullLogger<AppDataSourceProvisioner>.Instance);

            await provisioner.EnsureProvisionedAsync(
                tenantId,
                appInstanceId: 9001,
                appKey: "provision-test",
                operatorUserId: 1,
                preferredDataSourceId: null,
                cancellationToken: CancellationToken.None);

            var policy = await db.Queryable<AppDataRoutePolicy>()
                .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == 9001);
            Assert.NotNull(policy);
            Assert.Equal("AppOnly", policy!.Mode);
            Assert.True(appDbScopeFactory.GetCalledCount > 0);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task AppDbScopeFactory_WhenNoBinding_ShouldFallbackMainOnlyAndCreatePolicy()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        ServiceProvider? cacheProvider = null;
        AppDbScopeFactory? scopeFactory = null;
        try
        {
            db = CreateDb(dbPath);
            db.CodeFirst.InitTables(typeof(AppDataRoutePolicy), typeof(LowCodeApp), typeof(TenantDataSource), typeof(TenantAppDataSourceBinding));
            cacheProvider = CreateCacheProvider();
            var hybridCache = cacheProvider.GetRequiredService<IAtlasHybridCache>();
            var tenantId = new TenantId(Guid.Parse("62626262-6262-6262-6262-626262626262"));
            var app = new LowCodeApp(
                tenantId,
                "fallback-mainonly",
                "fallback-mainonly",
                null,
                null,
                null,
                dataSourceId: null,
                createdBy: 1,
                id: 9101,
                now: DateTimeOffset.UtcNow);
            await db.Insertable(app).ExecuteCommandAsync();
            scopeFactory = new AppDbScopeFactory(
                new NullConnectionFactory(),
                db,
                hybridCache,
                new SequentialIdGenerator(2000),
                NullLogger<AppDbScopeFactory>.Instance);

            var client = await scopeFactory.GetAppClientAsync(tenantId, 9101, CancellationToken.None);

            Assert.Same(db, client);
            var policy = await db.Queryable<AppDataRoutePolicy>()
                .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == 9101);
            Assert.NotNull(policy);
            Assert.Equal("MainOnly", policy!.Mode);
        }
        finally
        {
            scopeFactory?.Dispose();
            cacheProvider?.Dispose();
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task LowCodeAppCommandService_Create_ShouldInvokeProvisioner()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            db.CodeFirst.InitTables(typeof(LowCodeApp), typeof(AppManifest), typeof(TenantApplication), typeof(TenantDataSource));
            var tenantId = new TenantId(Guid.Parse("63636363-6363-6363-6363-636363636363"));
            var appRepository = new LowCodeAppRepository(db);
            var pageRepository = Substitute.For<ILowCodePageRepository>();
            var versionRepository = Substitute.For<ILowCodeAppVersionRepository>();
            var pageVersionRepository = Substitute.For<ILowCodePageVersionRepository>();
            var provisioner = new CaptureProvisioner();
            var service = new LowCodeAppCommandService(
                appRepository,
                pageRepository,
                versionRepository,
                pageVersionRepository,
                new SequentialIdGenerator(3000),
                provisioner,
                db);

            var request = new LowCodeAppCreateRequest(
                "lowcode-provision",
                "低代码供给联动",
                null,
                null,
                null,
                null);
            var appId = await service.CreateAsync(tenantId, 1, request, CancellationToken.None);

            Assert.Equal(appId, provisioner.LastAppInstanceId);
            Assert.Equal("lowcode-provision", provisioner.LastAppKey);
            Assert.Equal(tenantId, provisioner.LastTenantId);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    private static SqlSugarClient CreateDb(string dbPath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static ServiceProvider CreateCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<IOptions<AtlasHybridCacheOptions>>(Options.Create(new AtlasHybridCacheOptions()));
        services.AddSingleton<IAtlasHybridCache, AtlasHybridCache>();
        return services.BuildServiceProvider();
    }

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"atlas-app-provision-{Guid.NewGuid():N}.db");
    }

    private static void CleanupDbFile(string dbPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!File.Exists(dbPath))
            {
                return;
            }

            try
            {
                File.Delete(dbPath);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    private sealed class SequentialIdGenerator : IIdGeneratorAccessor
    {
        private long _current;

        public SequentialIdGenerator(long start)
        {
            _current = start;
        }

        public long NextId()
        {
            _current += 1;
            return _current;
        }
    }

    private sealed class NoopTenantDbConnectionFactory : ITenantDbConnectionFactory
    {
        public Task<string?> GetConnectionStringAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantDbConnectionInfo?>(null);

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, long tenantAppInstanceId, CancellationToken ct = default)
            => Task.FromResult<TenantDbConnectionInfo?>(null);

        public void InvalidateCache(string tenantId)
        {
        }

        public void InvalidateCache(string tenantId, long? tenantAppInstanceId)
        {
        }
    }

    private sealed class NullConnectionFactory : ITenantDbConnectionFactory
    {
        public Task<string?> GetConnectionStringAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, CancellationToken ct = default)
            => Task.FromResult<TenantDbConnectionInfo?>(null);

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, long tenantAppInstanceId, CancellationToken ct = default)
            => Task.FromResult<TenantDbConnectionInfo?>(null);

        public void InvalidateCache(string tenantId)
        {
        }

        public void InvalidateCache(string tenantId, long? tenantAppInstanceId)
        {
        }
    }

    private sealed class FakeAppDbScopeFactory : IAppDbScopeFactory
    {
        private readonly ISqlSugarClient _db;
        public int GetCalledCount { get; private set; }

        public FakeAppDbScopeFactory(ISqlSugarClient db)
        {
            _db = db;
        }

        public Task<ISqlSugarClient> GetAppClientAsync(TenantId tenantId, long appInstanceId, CancellationToken cancellationToken = default)
        {
            GetCalledCount++;
            return Task.FromResult(_db);
        }

        public Task<ISqlSugarClient?> TryGetAppClientAsync(TenantId tenantId, long appInstanceId, CancellationToken cancellationToken = default)
        {
            GetCalledCount++;
            return Task.FromResult<ISqlSugarClient?>(_db);
        }

        public void InvalidateAppClientCache(TenantId tenantId, long appInstanceId)
        {
        }
    }

    private sealed class CaptureProvisioner : IAppDataSourceProvisioner
    {
        public TenantId LastTenantId { get; private set; } = TenantId.Empty;
        public long LastAppInstanceId { get; private set; }
        public string LastAppKey { get; private set; } = string.Empty;

        public Task EnsureProvisionedAsync(
            TenantId tenantId,
            long appInstanceId,
            string appKey,
            long operatorUserId,
            long? preferredDataSourceId = null,
            CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            LastAppInstanceId = appInstanceId;
            LastAppKey = appKey;
            return Task.CompletedTask;
        }
    }
}
