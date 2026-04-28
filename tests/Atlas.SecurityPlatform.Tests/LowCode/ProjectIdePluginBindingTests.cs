using System.Text.Json;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.LowCode;

public sealed class ProjectIdePluginBindingTests : IDisposable
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"project-ide-plugin-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetGraphAsync_IncludesBoundPluginInGroupsAndSnapshot()
    {
        using var db = CreateDb(_dbPath);
        await CreateSchemaAsync(db);

        var appId = 1496214388233736192L;
        var app = new AppDefinition(Tenant, appId, "app-demo", "Demo", "web", "zh-CN");
        var page = new PageDefinition(Tenant, 2001, appId, "home", "Home", "/", "web", "free", 0);
        var plugin = new LowCodePluginDefinition(Tenant, 3001, "plg_draw", "绘画插件", "image", 100);
        var binding = new AiAppResourceBinding(Tenant, appId, "plugin", plugin.Id, null, 0, null, 4001);

        await db.Insertable(plugin).ExecuteCommandAsync();
        await db.Insertable(binding).ExecuteCommandAsync();

        var appRepo = Substitute.For<IAppDefinitionRepository>();
        appRepo.FindByIdAsync(Tenant, appId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AppDefinition?>(app));

        var pageRepo = Substitute.For<IPageDefinitionRepository>();
        pageRepo.ListByAppAsync(Tenant, appId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PageDefinition>>([page]));

        var variableRepo = Substitute.For<IAppVariableRepository>();
        variableRepo.ListByAppAsync(Tenant, appId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AppVariable>>([]));

        var appQuery = Substitute.For<IAppDefinitionQueryService>();

        var service = new ProjectIdeDependencyGraphService(
            appRepo,
            pageRepo,
            variableRepo,
            appQuery,
            db,
            NullLogger<ProjectIdeDependencyGraphService>.Instance);

        var graph = await service.GetGraphAsync(Tenant, appId, schemaJsonOverride: null, CancellationToken.None);

        Assert.NotNull(graph);
        var pluginGroup = Assert.Single(graph.Groups, group => group.ResourceType == "plugin");
        var pluginReference = Assert.Single(pluginGroup.References);
        Assert.True(pluginReference.Exists);
        Assert.Equal("plg_draw", pluginReference.ResourceId);
        Assert.Equal("绘画插件", pluginReference.DisplayName);
        Assert.Equal("/bindings/plugins/4001", pluginReference.ReferencePath);

        using var snapshotDoc = JsonDocument.Parse(graph.ResourceSnapshotJson);
        var pluginIds = snapshotDoc.RootElement
            .GetProperty("pluginVersions")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToArray();
        Assert.Contains("plg_draw", pluginIds);
    }

    private static SqlSugarClient CreateDb(string path)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = ApplyEntityService
            }
        });
    }

    private static async Task CreateSchemaAsync(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<AiAppResourceBinding, LowCodePluginDefinition, AiPlugin>();
        await Task.CompletedTask;
    }

    private static void ApplyEntityService(System.Reflection.PropertyInfo property, EntityColumnInfo column)
    {
        if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
            && property.PropertyType == typeof(Atlas.Core.Tenancy.TenantId))
        {
            column.IsIgnore = true;
            return;
        }

        if (property.Name == "Id" && property.PropertyType == typeof(long))
        {
            column.IsPrimarykey = true;
            column.IsIdentity = false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // ignore
        }
    }
}
