using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.LowCode;
using Atlas.Application.System.Abstractions;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class LowCodeAppCommandServiceVersionTests
{
    [Fact]
    public async Task PublishAndRollback_ShouldCreateSnapshotAndRestoreMetadata()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tenantId = new TenantId(Guid.Parse("55555555-5555-5555-5555-555555555555"));
            var appRepository = new LowCodeAppRepository(db);
            var pageRepository = new LowCodePageRepository(db);
            var versionRepository = new LowCodeAppVersionRepository(db);
            var pageVersionRepository = new LowCodePageVersionRepository(db);
            var idGenerator = new SequentialIdGenerator(9000);
            var service = new LowCodeAppCommandService(
                appRepository,
                pageRepository,
                versionRepository,
                pageVersionRepository,
                idGenerator,
                new NoopProvisioner(),
                db);

            var seedTime = DateTimeOffset.UtcNow;
            var app = new LowCodeApp(
                tenantId,
                "sales_portal",
                "销售门户",
                "初始版本",
                "销售",
                "shop",
                createdBy: 1,
                id: 1001,
                now: seedTime);
            app.UpdateConfig("{\"theme\":\"default\"}", 1, seedTime);
            app.Publish(1, seedTime);
            await appRepository.InsertAsync(app);

            var ordersPage = new LowCodePage(
                tenantId,
                app.Id,
                "orders",
                "订单列表",
                LowCodePageType.List,
                "{\"type\":\"page\",\"title\":\"订单列表\"}",
                "/orders",
                "订单查询",
                "unordered-list",
                1,
                0,
                createdBy: 1,
                id: 2001,
                now: seedTime);
            var detailPage = new LowCodePage(
                tenantId,
                app.Id,
                "order_detail",
                "订单详情",
                LowCodePageType.Detail,
                "{\"type\":\"page\",\"title\":\"订单详情\"}",
                "/orders/:id",
                "订单详情页",
                "file-search",
                2,
                0,
                createdBy: 1,
                id: 2002,
                now: seedTime);
            detailPage.Publish(1, seedTime);

            await pageRepository.InsertAsync(ordersPage);
            await pageRepository.InsertAsync(detailPage);

            await service.PublishAsync(tenantId, 1, app.Id);
            var (versionsAfterPublish, totalAfterPublish) = await versionRepository.GetPagedAsync(tenantId, app.Id, 1, 20);
            Assert.Equal(1, totalAfterPublish);

            var targetVersion = versionsAfterPublish.Single();
            Assert.Equal(1, targetVersion.Version);
            Assert.Equal("Publish", targetVersion.ActionType);

            app.Update("销售门户 V2", "变更版", "销售", "shop", 1, 1, DateTimeOffset.UtcNow);
            await appRepository.UpdateAsync(app);

            ordersPage.UpdateSchema("{\"type\":\"page\",\"title\":\"订单列表V2\"}", 1, DateTimeOffset.UtcNow);
            await pageRepository.UpdateAsync(ordersPage);
            await pageRepository.DeleteAsync(detailPage.Id);

            var rollbackVersion = await service.RollbackAsync(tenantId, 1, app.Id, targetVersion.Id);
            Assert.Equal(2, rollbackVersion);

            var restoredApp = await appRepository.GetByIdAsync(tenantId, app.Id);
            Assert.NotNull(restoredApp);
            Assert.Equal("销售门户", restoredApp!.Name);
            Assert.Equal(2, restoredApp.Version);
            Assert.Equal(LowCodeAppStatus.Published, restoredApp.Status);

            var restoredPages = await pageRepository.GetByAppIdAsync(tenantId, app.Id);
            Assert.Equal(2, restoredPages.Count);
            Assert.Contains(restoredPages, x => x.PageKey == "orders" && x.SchemaJson.Contains("订单列表"));
            Assert.Contains(restoredPages, x => x.PageKey == "order_detail" && x.IsPublished);

            var (versionsAfterRollback, totalAfterRollback) = await versionRepository.GetPagedAsync(tenantId, app.Id, 1, 20);
            Assert.Equal(2, totalAfterRollback);
            var latestVersion = versionsAfterRollback.OrderByDescending(x => x.Version).First();
            Assert.Equal("Rollback", latestVersion.ActionType);
            Assert.Equal(targetVersion.Id, latestVersion.SourceVersionId);
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

                  CREATE TABLE "LowCodeAppVersion"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "AppId" INTEGER NOT NULL,
                    "Version" INTEGER NOT NULL,
                    "SnapshotJson" TEXT NOT NULL,
                    "ActionType" TEXT NOT NULL,
                    "SourceVersionId" INTEGER NULL,
                    "Note" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "CreatedBy" INTEGER NOT NULL
                  );

                  CREATE TABLE "LowCodePageVersion"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "PageId" INTEGER NOT NULL,
                    "AppId" INTEGER NOT NULL,
                    "SnapshotVersion" INTEGER NOT NULL,
                    "PageKey" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "PageType" INTEGER NOT NULL,
                    "SchemaJson" TEXT NOT NULL,
                    "RoutePath" TEXT NULL,
                    "Description" TEXT NULL,
                    "Icon" TEXT NULL,
                    "SortOrder" INTEGER NOT NULL,
                    "ParentPageId" INTEGER NULL,
                    "PermissionCode" TEXT NULL,
                    "DataTableKey" TEXT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                  );
                  """;

        return db.Ado.ExecuteCommandAsync(sql);
    }

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"atlas-lowcode-version-{Guid.NewGuid():N}.db");
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

    private sealed class NoopProvisioner : IAppDataSourceProvisioner
    {
        public Task EnsureProvisionedAsync(
            TenantId tenantId,
            long appInstanceId,
            string appKey,
            long operatorUserId,
            long? preferredDataSourceId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
