using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Services.Platform;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class CapabilityRegistryTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task GetAllAsync_ShouldReturnCodeDefaults_WhenNoOverrides()
    {
        var dbPath = BuildTempDbPath();
        using var db = CreateDb(dbPath);

        try
        {
            await CreateSchemaAsync(db);
            var service = new CapabilityRegistry(db);

            var items = await service.GetAllAsync(Tenant);

            Assert.NotEmpty(items);
            Assert.Contains(items, item => item.CapabilityKey == "organization");
            Assert.Contains(items, item => item.CapabilityKey == "appbridge-command-center");
        }
        finally
        {
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task GetAllAsync_ShouldApplyEnabledOverride()
    {
        var dbPath = BuildTempDbPath();
        using var db = CreateDb(dbPath);

        try
        {
            await CreateSchemaAsync(db);
            var now = DateTimeOffset.UtcNow;
            var entity = new CapabilityManifest(
                Tenant,
                id: 1001,
                capabilityKey: "workflow",
                title: "Workflow V2",
                category: "workflow",
                updatedAt: now,
                updatedBy: 9527);
            entity.Update(
                title: "Workflow V2",
                category: "workflow",
                hostModesJson: "[\"platform\",\"app\"]",
                platformRoute: "/apps/{appId}/workflow-v2",
                appRoute: "/apps/{appKey}/workflow-v2",
                requiredPermissionsJson: "[\"workflows:view\"]",
                navigationJson: "{\"group\":\"workflow\",\"order\":5}",
                supportsExposure: true,
                supportedCommandsJson: "[\"workflow.deploy\"]",
                isEnabled: true,
                updatedAt: now,
                updatedBy: 9527);
            await db.Insertable(entity).ExecuteCommandAsync();

            var service = new CapabilityRegistry(db);
            var workflow = await service.GetByKeyAsync(Tenant, "workflow");

            Assert.NotNull(workflow);
            Assert.Equal("Workflow V2", workflow.Title);
            Assert.Equal("/apps/{appId}/workflow-v2", workflow.PlatformRoute);
            Assert.Equal(5, workflow.Navigation.Order);
        }
        finally
        {
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task GetAllAsync_ShouldRemoveCapability_WhenOverrideDisabled()
    {
        var dbPath = BuildTempDbPath();
        using var db = CreateDb(dbPath);

        try
        {
            await CreateSchemaAsync(db);
            var now = DateTimeOffset.UtcNow;
            var entity = new CapabilityManifest(
                Tenant,
                id: 1002,
                capabilityKey: "agent",
                title: "Agent",
                category: "ai",
                updatedAt: now,
                updatedBy: 9527);
            entity.Update(
                title: "Agent",
                category: "ai",
                hostModesJson: "[\"platform\",\"app\"]",
                platformRoute: "/ai/agents",
                appRoute: "/apps/{appKey}/agents",
                requiredPermissionsJson: "[\"ai:agents:view\"]",
                navigationJson: "{\"group\":\"ai\",\"order\":200}",
                supportsExposure: true,
                supportedCommandsJson: "[\"agent.publish\"]",
                isEnabled: false,
                updatedAt: now,
                updatedBy: 9527);
            await db.Insertable(entity).ExecuteCommandAsync();

            var service = new CapabilityRegistry(db);
            var items = await service.GetAllAsync(Tenant);

            Assert.DoesNotContain(items, item => item.CapabilityKey == "agent");
        }
        finally
        {
            CleanupDbFile(dbPath);
        }
    }

    private static async Task CreateSchemaAsync(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<CapabilityManifest>();
        await Task.CompletedTask;
    }

    private static SqlSugarClient CreateDb(string dbPath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = SqlSugar.DbType.Sqlite,
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

    private static string BuildTempDbPath()
        => Path.Combine(Path.GetTempPath(), $"capability-registry-{Guid.NewGuid():N}.db");

    private static void CleanupDbFile(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return;
        }

        try
        {
            File.Delete(dbPath);
        }
        catch
        {
            // ignore cleanup failure
        }
    }
}
