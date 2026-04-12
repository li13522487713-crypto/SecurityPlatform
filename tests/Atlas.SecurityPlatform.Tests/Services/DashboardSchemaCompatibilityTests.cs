using System.Reflection;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.LowCode;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class DashboardSchemaCompatibilityTests
{
    [Fact]
    public async Task LegacyDashboardSchema_ShouldBeAligned_AndAllowNullCanvasColumns()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateLegacyDashboardSchemaAsync(db);

            Assert.True(IsColumnNotNull(db, "DashboardDefinition", "CanvasWidth"));
            Assert.True(IsColumnNotNull(db, "DashboardDefinition", "CanvasHeight"));

            await InvokeDashboardSchemaAlignmentAsync(db);

            Assert.False(IsColumnNotNull(db, "DashboardDefinition", "CanvasWidth"));
            Assert.False(IsColumnNotNull(db, "DashboardDefinition", "CanvasHeight"));

            var service = new DashboardService(db, new SequentialIdGenerator(1000));
            var tenantId = new TenantId(Guid.Parse("71717171-7171-7171-7171-717171717171"));
            var request = new DashboardDefinitionCreateRequest(
                "兼容性测试仪表盘",
                null,
                null,
                "{}",
                false,
                null,
                null);

            var id = await service.CreateAsync(tenantId, 1, request, CancellationToken.None);
            var created = await db.Queryable<DashboardDefinition>()
                .FirstAsync(item => item.Id == id && item.TenantIdValue == tenantId.Value);

            Assert.NotNull(created);
            Assert.Null(created!.CanvasWidth);
            Assert.Null(created.CanvasHeight);
            Assert.False(created.IsLargeScreen);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    private static async Task InvokeDashboardSchemaAlignmentAsync(ISqlSugarClient db)
    {
        var method = typeof(DatabaseInitializerHostedService).GetMethod(
            "EnsureDashboardDefinitionSchemaAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var task = method!.Invoke(null, [db, CancellationToken.None]) as Task;
        Assert.NotNull(task);
        await task!;
    }

    private static async Task CreateLegacyDashboardSchemaAsync(ISqlSugarClient db)
    {
        const string sql =
            """
            CREATE TABLE "DashboardDefinition"(
                "Name" varchar(255) NOT NULL,
                "Description" varchar(255) NOT NULL,
                "Category" varchar(255) NOT NULL,
                "LayoutJson" varchar(255) NOT NULL,
                "Version" integer NOT NULL,
                "Status" integer NOT NULL,
                "IsLargeScreen" bit NOT NULL,
                "CanvasWidth" integer NOT NULL,
                "CanvasHeight" integer NOT NULL,
                "ThemeJson" varchar(255) NOT NULL,
                "CreatedAt" datetime NOT NULL,
                "UpdatedAt" datetime NOT NULL,
                "CreatedBy" bigint NOT NULL,
                "UpdatedBy" bigint NOT NULL,
                "TenantIdValue" uniqueidentifier NOT NULL,
                "Id" bigint NOT NULL
            );
            """;

        await db.Ado.ExecuteCommandAsync(sql);
    }

    private static bool IsColumnNotNull(ISqlSugarClient db, string tableName, string columnName)
    {
        var column = db.DbMaintenance.GetColumnInfosByTableName(tableName, false)
            .FirstOrDefault(item => string.Equals(item.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(column);
        return !column!.IsNullable;
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

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"atlas-dashboard-schema-{Guid.NewGuid():N}.db");
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

        public SequentialIdGenerator(long seed)
        {
            _current = seed;
        }

        public long NextId()
        {
            _current += 1;
            return _current;
        }
    }
}
