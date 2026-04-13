using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Caching;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Services.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class NavigationProjectionServiceTests
{
    [Fact]
    public async Task GetWorkspaceProjectionAsync_ShouldIncludeAppCapabilities_WhenAppSetupSeededPermissionsExist()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        ServiceProvider? cacheProvider = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            cacheProvider = CreateCacheProvider();
            var cache = cacheProvider.GetRequiredService<IAtlasHybridCache>();
            var registry = Substitute.For<ICapabilityRegistry>();
            registry.GetAllAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
                .Returns(new[]
                {
                    new CapabilityManifestItem(
                        CapabilityKey: "organization",
                        Title: "Organization",
                        Category: "core",
                        HostModes: ["app"],
                        PlatformRoute: null,
                        AppRoute: "/apps/{appKey}/capabilities/organization",
                        RequiredPermissions: ["departments:view"],
                        Navigation: new CapabilityNavigationSuggestion("organization", 100),
                        SupportsExposure: true,
                        SupportedCommands: [],
                        IsEnabled: true)
                });

            var tenantId = new TenantId(Guid.Parse("88888888-8888-8888-8888-888888888888"));
            const long userId = 1001;
            const long roleId = 2001;
            const long appId = 4001;

            await db.Insertable(new LowCodeApp(
                    tenantId,
                    "dev-app",
                    "开发应用",
                    description: null,
                    category: null,
                    icon: null,
                    createdBy: 1,
                    id: appId,
                    now: DateTimeOffset.UtcNow))
                .ExecuteCommandAsync();

            await db.Ado.ExecuteCommandAsync(
                """
                INSERT INTO "AppRole" ("TenantIdValue","Id","AppId","Name","Code","Description","IsSystem","DataScope","DeptIds")
                VALUES (@tenantIdValue, @roleId, @appId, '安全管理员', 'SecurityAdmin', NULL, 0, 'CurrentApp', NULL);
                """,
                new SugarParameter("@tenantIdValue", tenantId.Value.ToString()),
                new SugarParameter("@roleId", roleId),
                new SugarParameter("@appId", appId));

            await db.Ado.ExecuteCommandAsync(
                """
                INSERT INTO "AppUserRole" ("TenantIdValue","Id","AppId","UserId","RoleId")
                VALUES (@tenantIdValue, 5001, @appId, @userId, @roleId);
                """,
                new SugarParameter("@tenantIdValue", tenantId.Value.ToString()),
                new SugarParameter("@appId", appId),
                new SugarParameter("@userId", userId),
                new SugarParameter("@roleId", roleId));

            await db.Ado.ExecuteCommandAsync(
                """
                INSERT INTO "AppRolePermission" ("TenantIdValue","Id","AppId","RoleId","PermissionCode")
                VALUES (@tenantIdValue, 5002, @appId, @roleId, 'departments:view');
                """,
                new SugarParameter("@tenantIdValue", tenantId.Value.ToString()),
                new SugarParameter("@appId", appId),
                new SugarParameter("@roleId", roleId));

            var service = new NavigationProjectionService(db, registry, cache);

            var result = await service.GetWorkspaceProjectionAsync(
                tenantId,
                appId,
                userId,
                isPlatformAdmin: false,
                CancellationToken.None);

            Assert.Single(result.Groups);
            Assert.Single(result.Groups[0].Items);
            Assert.Equal("/apps/dev-app/capabilities/organization", result.Groups[0].Items[0].Path);
        }
        finally
        {
            cacheProvider?.Dispose();
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

    private static Task CreateSchemaAsync(SqlSugarClient db)
    {
        var sql = """
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

                  CREATE TABLE "AppRole"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppId" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Code" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "IsSystem" INTEGER NOT NULL,
                    "DataScope" TEXT NOT NULL,
                    "DeptIds" TEXT NULL
                  );

                  CREATE TABLE "AppUserRole"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppId" INTEGER NOT NULL,
                    "UserId" INTEGER NOT NULL,
                    "RoleId" INTEGER NOT NULL
                  );

                  CREATE TABLE "AppRolePermission"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppId" INTEGER NOT NULL,
                    "RoleId" INTEGER NOT NULL,
                    "PermissionCode" TEXT NOT NULL
                  );
                  """;

        return db.Ado.ExecuteCommandAsync(sql);
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
        return Path.Combine(Path.GetTempPath(), $"atlas-nav-projection-{Guid.NewGuid():N}.db");
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
}
