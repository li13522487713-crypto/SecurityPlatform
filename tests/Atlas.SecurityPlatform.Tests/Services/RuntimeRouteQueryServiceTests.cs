using Atlas.Application.Approval.Abstractions;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Application.Platform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class RuntimeRouteQueryServiceTests
{
    [Fact]
    public async Task GetRuntimeMenuAndPage_ShouldFallbackToDraftDataWhenAppIsNotReleased()
    {
        var mainDbPath = CreateTempDbPath();
        var appDbPath = CreateTempDbPath();
        SqlSugarClient? mainDb = null;
        SqlSugarClient? appDb = null;
        ServiceProvider? cacheProvider = null;
        AppDbScopeFactory? scopeFactory = null;
        try
        {
            mainDb = CreateDb(mainDbPath);
            appDb = CreateDb(appDbPath);
            cacheProvider = CreateCacheProvider();
            var hybridCache = cacheProvider.GetRequiredService<IAtlasHybridCache>();
            scopeFactory = new AppDbScopeFactory(
                new FakeTenantDbConnectionFactory(new TenantDbConnectionInfo($"Data Source={appDbPath}", "Sqlite")),
                mainDb,
                hybridCache);

            await CreateMainSchemaAsync(mainDb);

            var tenantId = new TenantId(Guid.Parse("78787878-7878-7878-7878-787878787878"));
            const long manifestId = 1001;
            const string appKey = "dev-app";

            var manifest = new AppManifest(
                tenantId,
                manifestId,
                appKey,
                "开发应用",
                createdBy: 1,
                now: DateTimeOffset.UtcNow);
            await mainDb.Insertable(manifest).ExecuteCommandAsync();

            var page = new LowCodePage(
                tenantId,
                manifestId,
                "dashboard",
                "仪表盘",
                LowCodePageType.List,
                "{\"type\":\"page\"}",
                "/dev-app/dashboard",
                "开发页",
                "appstore",
                10,
                0,
                createdBy: 1,
                id: 2001,
                now: DateTimeOffset.UtcNow);
            await mainDb.Insertable(page).ExecuteCommandAsync();

            var runtimeDb = await scopeFactory.GetAppClientAsync(tenantId, manifestId, CancellationToken.None);
            var route = new RuntimeRoute(tenantId, 3001, manifestId, appKey, "dashboard", 7);
            await runtimeDb.Insertable(route).ExecuteCommandAsync();

            var service = new RuntimeRouteQueryService(
                mainDb,
                scopeFactory,
                Substitute.For<IApprovalOperationService>());

            var menu = await service.GetRuntimeMenuAsync(tenantId, appKey, CancellationToken.None);
            Assert.Equal(appKey, menu.AppKey);
            Assert.Single(menu.Items);
            var menuItem = menu.Items[0];
            Assert.Equal("dashboard", menuItem.PageKey);
            Assert.Equal("仪表盘", menuItem.Title);
            Assert.Equal("/dev-app/dashboard", menuItem.RoutePath);
            Assert.Equal("appstore", menuItem.Icon);
            Assert.Equal(10, menuItem.SortOrder);

            var runtimePage = await service.GetRuntimePageAsync(tenantId, appKey, "dashboard", CancellationToken.None);
            Assert.NotNull(runtimePage);
            Assert.Equal(appKey, runtimePage!.AppKey);
            Assert.Equal("dashboard", runtimePage.PageKey);
            Assert.Equal(7, runtimePage.SchemaVersion);
            Assert.True(runtimePage.IsActive);
        }
        finally
        {
            scopeFactory?.Dispose();
            cacheProvider?.Dispose();
            appDb?.Dispose();
            mainDb?.Dispose();
            CleanupDbFile(mainDbPath);
            CleanupDbFile(appDbPath);
        }
    }

    [Fact]
    public async Task GetRuntimeMenuAndPage_ShouldFallbackToLowCodeAppCreatedBySetup()
    {
        var mainDbPath = CreateTempDbPath();
        var appDbPath = CreateTempDbPath();
        SqlSugarClient? mainDb = null;
        SqlSugarClient? appDb = null;
        ServiceProvider? cacheProvider = null;
        AppDbScopeFactory? scopeFactory = null;
        try
        {
            mainDb = CreateDb(mainDbPath);
            appDb = CreateDb(appDbPath);
            cacheProvider = CreateCacheProvider();
            var hybridCache = cacheProvider.GetRequiredService<IAtlasHybridCache>();
            scopeFactory = new AppDbScopeFactory(
                new FakeTenantDbConnectionFactory(new TenantDbConnectionInfo($"Data Source={appDbPath}", "Sqlite")),
                mainDb,
                hybridCache);

            await CreateMainSchemaAsync(mainDb);

            var tenantId = new TenantId(Guid.Parse("79797979-7979-7979-7979-797979797979"));
            const long appId = 1101;
            const string appKey = "dev-app";

            var app = new LowCodeApp(
                tenantId,
                appKey,
                "初始化向导应用",
                description: null,
                category: null,
                icon: null,
                createdBy: 1,
                id: appId,
                now: DateTimeOffset.UtcNow);
            await mainDb.Insertable(app).ExecuteCommandAsync();

            var page = new LowCodePage(
                tenantId,
                appId,
                "dashboard",
                "工作台",
                LowCodePageType.List,
                "{\"type\":\"page\"}",
                "/dev-app/dashboard",
                "初始化页面",
                "appstore",
                5,
                0,
                createdBy: 1,
                id: 2101,
                now: DateTimeOffset.UtcNow);
            await mainDb.Insertable(page).ExecuteCommandAsync();

            var runtimeDb = await scopeFactory.GetAppClientAsync(tenantId, appId, CancellationToken.None);
            await runtimeDb.Insertable(new RuntimeRoute(tenantId, 3101, appId, appKey, "dashboard", 3))
                .ExecuteCommandAsync();

            var service = new RuntimeRouteQueryService(
                mainDb,
                scopeFactory,
                Substitute.For<IApprovalOperationService>());

            var menu = await service.GetRuntimeMenuAsync(tenantId, appKey, CancellationToken.None);
            Assert.Single(menu.Items);
            Assert.Equal("工作台", menu.Items[0].Title);
            Assert.Equal("/dev-app/dashboard", menu.Items[0].RoutePath);

            var runtimePage = await service.GetRuntimePageAsync(tenantId, appKey, "dashboard", CancellationToken.None);
            Assert.NotNull(runtimePage);
            Assert.Equal(3, runtimePage!.SchemaVersion);
        }
        finally
        {
            scopeFactory?.Dispose();
            cacheProvider?.Dispose();
            appDb?.Dispose();
            mainDb?.Dispose();
            CleanupDbFile(mainDbPath);
            CleanupDbFile(appDbPath);
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

    private static Task CreateMainSchemaAsync(SqlSugarClient db)
    {
        var sql = """
                  CREATE TABLE "AppManifest"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppKey" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Description" TEXT NOT NULL,
                    "Category" TEXT NOT NULL,
                    "Icon" TEXT NOT NULL,
                    "ConfigJson" TEXT NOT NULL,
                    "DataSourceId" INTEGER NULL,
                    "Version" INTEGER NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedBy" INTEGER NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "PublishedBy" INTEGER NULL,
                    "PublishedAt" TEXT NULL
                  );

                  CREATE TABLE "LowCodeApp"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppKey" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "Category" TEXT NULL,
                    "Icon" TEXT NULL,
                    "DataSourceId" INTEGER NULL,
                    "Version" INTEGER NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "UpdatedBy" INTEGER NOT NULL,
                    "PublishedAt" TEXT NULL,
                    "PublishedBy" INTEGER NULL,
                    "ConfigJson" TEXT NULL,
                    "MenuConfigJson" TEXT NULL
                  );

                  CREATE TABLE "LowCodePage"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppId" INTEGER NOT NULL,
                    "PageKey" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "PageType" INTEGER NOT NULL,
                    "SchemaJson" TEXT NOT NULL,
                    "RoutePath" TEXT NULL,
                    "Description" TEXT NULL,
                    "Icon" TEXT NULL,
                    "SortOrder" INTEGER NOT NULL,
                    "ParentPageId" INTEGER NULL,
                    "Version" INTEGER NOT NULL,
                    "IsPublished" INTEGER NOT NULL,
                    "PublishedSchemaJson" TEXT NULL,
                    "PublishedVersion" INTEGER NULL,
                    "PublishedAt" TEXT NULL,
                    "PublishedBy" INTEGER NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "UpdatedBy" INTEGER NOT NULL,
                    "PermissionCode" TEXT NULL,
                    "DataTableKey" TEXT NULL
                  );

                  CREATE TABLE "AppDataRoutePolicy"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppInstanceId" INTEGER NOT NULL,
                    "Mode" TEXT NOT NULL,
                    "ReadOnlyWindow" INTEGER NOT NULL,
                    "DualWriteEnabled" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "UpdatedBy" INTEGER NOT NULL
                  );

                  CREATE TABLE "AppMigrationTask"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "TenantAppInstanceId" INTEGER NOT NULL,
                    "DataSourceId" INTEGER NOT NULL,
                    "Status" TEXT NOT NULL,
                    "Phase" TEXT NOT NULL,
                    "TotalItems" INTEGER NOT NULL,
                    "CompletedItems" INTEGER NOT NULL,
                    "FailedItems" INTEGER NOT NULL,
                    "ProgressPercent" REAL NOT NULL,
                    "CurrentObjectName" TEXT NULL,
                    "CurrentBatchNo" INTEGER NULL,
                    "ReadOnlyWindow" INTEGER NOT NULL,
                    "EnableDualWrite" INTEGER NOT NULL,
                    "EnableRollback" INTEGER NOT NULL,
                    "ErrorSummary" TEXT NULL,
                    "SchemaRepairLog" TEXT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedBy" INTEGER NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "StartedAt" TEXT NULL,
                    "FinishedAt" TEXT NULL
                  );

                  CREATE TABLE "AppRelease"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "ManifestId" INTEGER NOT NULL,
                    "Version" INTEGER NOT NULL,
                    "ReleaseNote" TEXT NOT NULL,
                    "SnapshotJson" TEXT NOT NULL,
                    "RollbackPointId" INTEGER NULL,
                    "Status" INTEGER NOT NULL,
                    "ReleasedBy" INTEGER NOT NULL,
                    "ReleasedAt" TEXT NOT NULL,
                    "ArtifactId" TEXT NULL,
                    "Checksum" TEXT NULL,
                    "InstallSpec" TEXT NULL,
                    "RollbackMetadata" TEXT NULL,
                    "NavigationSnapshotJson" TEXT NULL,
                    "ExposureCatalogSnapshotJson" TEXT NULL
                  );
                  """;

        return db.Ado.ExecuteCommandAsync(sql);
    }

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"atlas-runtime-route-{Guid.NewGuid():N}.db");
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

    private static ServiceProvider CreateCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<IOptions<AtlasHybridCacheOptions>>(Options.Create(new AtlasHybridCacheOptions()));
        services.AddSingleton<IAtlasHybridCache, AtlasHybridCache>();
        return services.BuildServiceProvider();
    }

    private sealed class FakeTenantDbConnectionFactory : ITenantDbConnectionFactory
    {
        private readonly TenantDbConnectionInfo _connectionInfo;

        public FakeTenantDbConnectionFactory(TenantDbConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
        }

        public Task<string?> GetConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<string?>(_connectionInfo.ConnectionString);
        }

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<TenantDbConnectionInfo?>(_connectionInfo);
        }

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, long tenantAppInstanceId, CancellationToken ct = default)
        {
            return Task.FromResult<TenantDbConnectionInfo?>(_connectionInfo);
        }

        public void InvalidateCache(string tenantId)
        {
        }

        public void InvalidateCache(string tenantId, long? tenantAppInstanceId)
        {
        }
    }
}
