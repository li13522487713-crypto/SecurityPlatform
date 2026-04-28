using System.Text.Json;
using Atlas.AppHost.Controllers;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Controllers.Ai;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Atlas.AppHost.Tests;

public sealed class CozeNativeAgentCompatControllerTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task GenerateStoreCategory_ShouldSelectMatchedCategory()
    {
        var marketplaceService = Substitute.For<IAiMarketplaceService>();
        marketplaceService.GetCategoriesAsync(TestTenant, Arg.Any<CancellationToken>())
            .Returns([
                new AiProductCategoryItem(10, "Automation", "automation", null, 2, true),
                new AiProductCategoryItem(20, "Knowledge", "knowledge", null, 1, true)
            ]);

        var controller = BuildPlaygroundController(services =>
        {
            services.AddSingleton(marketplaceService);
        });

        var result = await controller.GenerateStoreCategory(
            new CozeGenerateStoreCategoryCompatRequest(
                "Knowledge Copilot",
                "Knowledge assistant",
                "Help users search knowledge"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"category_id\":\"20\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MoveDraftBot_Preview_ShouldReturnAsyncTask()
    {
        var queryService = Substitute.For<IAgentQueryService>();
        queryService.GetByIdAsync(TestTenant, 100, Arg.Any<CancellationToken>())
            .Returns(new AgentDetail(
                100,
                "Mover",
                "desc",
                null,
                "prompt",
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<AgentKnowledgeBindingItem>(),
                Array.Empty<AgentDatabaseBindingItem>(),
                Array.Empty<AgentVariableBindingItem>(),
                Array.Empty<long>(),
                Array.Empty<long>(),
                null,
                null,
                null,
                null,
                null,
                null,
                "Draft",
                1,
                DateTime.UtcNow,
                DateTime.UtcNow,
                null,
                0,
                false,
                false,
                false,
                0,
                Array.Empty<long>(),
                Array.Empty<AgentPluginBindingItem>(),
                null,
                1));

        var controller = BuildPlaygroundController(services =>
        {
            services.AddSingleton(queryService);
            services.AddSingleton(Substitute.For<IAgentCommandService>());
        });

        var result = await controller.MoveDraftBot(
            new CozeMoveDraftBotCompatRequest("100", "2", "1", 3),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"bot_status\":1", payload, StringComparison.Ordinal);
        Assert.Contains("\"TargetSpaceId\":\"2\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"OriSpaceId\":\"1\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetImagexShortUrl_ShouldResolveAtlasFileUri()
    {
        var fileStorage = Substitute.For<IFileStorageService>();
        fileStorage.GenerateSignedUrlAsync(TestTenant, 12, 600, Arg.Any<CancellationToken>())
            .Returns(new FileSignedUrlResult("/api/v1/files/signed/12?sig=test", DateTimeOffset.UtcNow.AddMinutes(10)));

        var controller = BuildPlaygroundController(services =>
        {
            services.AddSingleton(fileStorage);
        });

        var result = await controller.GetImagexShortUrl(
            new CozeGetImagexShortUrlCompatRequest(["atlas-file:12"], null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"atlas-file:12\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"/api/v1/files/signed/12?sig=test\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPublishIntelligenceList_ShouldReturnPublishedBot()
    {
        var queryService = Substitute.For<IAgentQueryService>();
        queryService.GetPagedAsync(TestTenant, null, null, 1, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new Atlas.Core.Models.PagedResult<AgentListItem>(
                [
                    new AgentListItem(101, "Published Bot", "desc", null, "Published", null, DateTime.UtcNow, 3)
                ],
                1,
                1,
                20));

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(queryService);
        });

        var result = await controller.GetPublishIntelligenceList(
            new CozeGetPublishIntelligenceListRequest(1, "1", null, null, null, null, 20, null, ["101"]),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"Published Bot\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"trigger\":false", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPublishRecordDetail_ShouldReturnPublishedRecord()
    {
        var queryService = Substitute.For<IAgentQueryService>();
        var publicationService = Substitute.For<IAgentPublicationService>();
        queryService.GetByIdAsync(TestTenant, 101, Arg.Any<CancellationToken>())
            .Returns(new AgentDetail(
                101,
                "Published Bot",
                "desc",
                null,
                "prompt",
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<AgentKnowledgeBindingItem>(),
                Array.Empty<AgentDatabaseBindingItem>(),
                Array.Empty<AgentVariableBindingItem>(),
                Array.Empty<long>(),
                Array.Empty<long>(),
                null,
                null,
                null,
                null,
                null,
                null,
                "Published",
                1,
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow,
                3,
                false,
                false,
                false,
                0,
                Array.Empty<long>(),
                Array.Empty<AgentPluginBindingItem>(),
                "{\"connectors\":{\"2001\":{\"ConfigStatus\":1,\"ConnectorStatus\":0,\"IsLastPublished\":true,\"ShareLink\":\"https://example.test/publish/102\",\"Detail\":{\"endpoint_url\":\"https://example.test\"}}}}",
                1));
        publicationService.GetByAgentAsync(TestTenant, 101, Arg.Any<CancellationToken>())
            .Returns([
                new AgentPublicationListItem(9001, 101, 3, true, "embed", DateTime.UtcNow.AddHours(1), "release-note", 1, DateTime.UtcNow, DateTime.UtcNow, null)
            ]);

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(queryService);
            services.AddSingleton(publicationService);
        });

        var result = await controller.GetPublishRecordDetail(
            new CozeGetPublishRecordDetailRequest("101", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"publish_record_id\":\"bot-101-v3\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"publish_status\":5", payload, StringComparison.Ordinal);
        Assert.Contains("\"connector_name\":\"Generic Endpoint\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"endpoint_url\":\"https://example.test\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPublishRecordList_ShouldReturnLatestPublishedRecord()
    {
        var queryService = Substitute.For<IAgentQueryService>();
        var publicationService = Substitute.For<IAgentPublicationService>();
        queryService.GetByIdAsync(TestTenant, 102, Arg.Any<CancellationToken>())
            .Returns(new AgentDetail(
                102,
                "Published Bot 2",
                "desc",
                null,
                "prompt",
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<AgentKnowledgeBindingItem>(),
                Array.Empty<AgentDatabaseBindingItem>(),
                Array.Empty<AgentVariableBindingItem>(),
                Array.Empty<long>(),
                Array.Empty<long>(),
                null,
                null,
                null,
                null,
                null,
                null,
                "Published",
                1,
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow,
                2,
                false,
                false,
                false,
                0,
                Array.Empty<long>(),
                Array.Empty<AgentPluginBindingItem>(),
                null,
                1));
        publicationService.GetByAgentAsync(TestTenant, 102, Arg.Any<CancellationToken>())
            .Returns([
                new AgentPublicationListItem(9101, 102, 2, true, "embed-2", DateTime.UtcNow.AddHours(1), "current release", 1, DateTime.UtcNow, DateTime.UtcNow, null),
                new AgentPublicationListItem(9100, 102, 1, false, "embed-1", DateTime.UtcNow.AddHours(1), "previous release", 1, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow)
            ]);

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(queryService);
            services.AddSingleton(publicationService);
        });

        var result = await controller.GetPublishRecordList(
            new CozeGetPublishRecordListRequest("102"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"version_number\":\"v2\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"version_number\":\"v1\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"publish_record_id\":\"bot-102-v2\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPublishTriggerList_ShouldReturnAgentTriggers()
    {
        var triggerService = Substitute.For<IAgentTriggerService>();
        triggerService.ListAsync(TestTenant, 103, Arg.Any<CancellationToken>())
            .Returns([
                new AgentTriggerDto("701", "103", "Morning digest", "schedule", "{\"cron\":\"0 8 * * *\"}", true, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1)),
                new AgentTriggerDto("702", "103", "Webhook order", "webhook", "{\"path\":\"/hook/order\"}", false, DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddHours(-2))
            ]);

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(triggerService);
        });

        var result = await controller.GetPublishTriggerList(
            new CozeGetPublishTriggerListRequest("103"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"Morning digest\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"trigger_type\":\"schedule\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"total\":2", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdatePublishTrigger_ShouldForwardMergedPayload()
    {
        var triggerService = Substitute.For<IAgentTriggerService>();
        triggerService.ListAsync(TestTenant, 103, Arg.Any<CancellationToken>())
            .Returns([
                new AgentTriggerDto("701", "103", "Morning digest", "schedule", "{\"cron\":\"0 8 * * *\"}", true, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1))
            ]);

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(triggerService);
        });

        var result = await controller.UpdatePublishTrigger(
            new CozeUpdatePublishTriggerRequest("103", "701", null, null, null, false),
            CancellationToken.None);

        await triggerService.Received(1).UpdateAsync(
            TestTenant,
            103,
            701,
            Arg.Is<AgentTriggerUpsertRequest>(item =>
                item.Name == "Morning digest"
                && item.TriggerType == "schedule"
                && item.ConfigJson == "{\"cron\":\"0 8 * * *\"}"
                && item.IsEnabled == false),
            Arg.Any<CancellationToken>());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"trigger_id\":\"701\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"enabled\":false", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePublishTrigger_ShouldReturnCreatedTrigger()
    {
        var triggerService = Substitute.For<IAgentTriggerService>();
        triggerService.CreateAsync(TestTenant, 103, 1, Arg.Any<AgentTriggerUpsertRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AgentTriggerDto("703", "103", "Evening digest", "schedule", "{\"cron\":\"0 20 * * *\"}", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(triggerService);
        });

        var result = await controller.CreatePublishTrigger(
            new CozeCreatePublishTriggerRequest("103", "Evening digest", "schedule", "{\"cron\":\"0 20 * * *\"}", true),
            CancellationToken.None);

        await triggerService.Received(1).CreateAsync(
            TestTenant,
            103,
            1,
            Arg.Is<AgentTriggerUpsertRequest>(item =>
                item.Name == "Evening digest"
                && item.TriggerType == "schedule"
                && item.ConfigJson == "{\"cron\":\"0 20 * * *\"}"
                && item.IsEnabled),
            Arg.Any<CancellationToken>());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"trigger_id\":\"703\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"Evening digest\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPublishLogList_ShouldReturnRuntimeLogs()
    {
        var logService = Substitute.For<IRuntimeMessageLogService>();
        logService.QueryAsync(TestTenant, Arg.Any<RuntimeMessageLogQuery>(), Arg.Any<CancellationToken>())
            .Returns([
                new RuntimeMessageLogEntryDto("log-1", "agent", "publish", "session-1", "workflow-1", "103", "trace-1", null, DateTimeOffset.UtcNow),
                new RuntimeMessageLogEntryDto("log-2", "dispatch", "message", "session-2", null, "103", "trace-2", null, DateTimeOffset.UtcNow.AddMinutes(-5))
            ]);

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(logService);
        });

        var result = await controller.GetPublishLogList(
            new CozeGetPublishLogListRequest("103", null, null, null, null, 1, 20),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"log_id\":\"log-1\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"dispatch\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"total\":2", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPublishLogList_ShouldFilterBySourceAndKind()
    {
        var logService = Substitute.For<IRuntimeMessageLogService>();
        logService.QueryAsync(TestTenant, Arg.Any<RuntimeMessageLogQuery>(), Arg.Any<CancellationToken>())
            .Returns([
                new RuntimeMessageLogEntryDto("log-1", "agent", "publish", "session-1", "workflow-1", "103", "trace-1", null, DateTimeOffset.UtcNow),
                new RuntimeMessageLogEntryDto("log-2", "dispatch", "message", "session-2", null, "103", "trace-2", null, DateTimeOffset.UtcNow.AddMinutes(-5))
            ]);

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(logService);
        });

        var result = await controller.GetPublishLogList(
            new CozeGetPublishLogListRequest("103", null, null, "dispatch", "message", 1, 20),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain("\"log_id\":\"log-1\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"log_id\":\"log-2\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"total\":1", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeletePublishTrigger_ShouldReturnDeletedFlag()
    {
        var triggerService = Substitute.For<IAgentTriggerService>();

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(triggerService);
        });

        var result = await controller.DeletePublishTrigger(
            new CozeDeletePublishTriggerRequest("103", "701"),
            CancellationToken.None);

        await triggerService.Received(1).DeleteAsync(TestTenant, 103, 701, Arg.Any<CancellationToken>());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"deleted\":true", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePublishTrigger_ShouldFailForInvalidProjectId()
    {
        var controller = BuildIntelligenceController(_ => { });

        var result = await controller.CreatePublishTrigger(
            new CozeCreatePublishTriggerRequest("0", "Bad trigger", "schedule", "{}", true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"code\":1", payload, StringComparison.Ordinal);
        Assert.Contains("\"project_id is invalid\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdatePublishTrigger_ShouldFailWhenTriggerMissing()
    {
        var triggerService = Substitute.For<IAgentTriggerService>();
        triggerService.ListAsync(TestTenant, 103, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AgentTriggerDto>());

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(triggerService);
        });

        var result = await controller.UpdatePublishTrigger(
            new CozeUpdatePublishTriggerRequest("103", "701", null, null, null, false),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"code\":1", payload, StringComparison.Ordinal);
        Assert.Contains("\"trigger not found\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void GetUserQueryCollectOption_ShouldReturnSupportConnectors()
    {
        var controller = BuildPlaygroundController(_ => { });

        var result = controller.GetUserQueryCollectOption();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"support_connectors\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"private_policy_template\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateUserQueryCollectPolicy_ShouldReturnPolicyLink()
    {
        var controller = BuildPlaygroundController(_ => { });

        var result = controller.GenerateUserQueryCollectPolicy(
            new CozeGenerateUserQueryCollectPolicyCompatRequest("100", "Atlas Admin", "admin@example.com"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"policy_link\":\"/open/policy/bot/100", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void GetConnectorAuthState_ShouldReturnStatePayload()
    {
        var controller = BuildDeveloperController();

        var result = controller.GetConnectorAuthState("3001");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"connector_id\":\"3001\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"origin\":\"publish\"", payload, StringComparison.Ordinal);
    }

    private static AppWebCozePlaygroundGatewayController BuildPlaygroundController(Action<IServiceCollection>? configureServices)
    {
        var workspacePortalService = Substitute.For<IWorkspacePortalService>();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenantId().Returns(TestTenant);
        var currentUserAccessor = Substitute.For<ICurrentUserAccessor>();
        currentUserAccessor.GetCurrentUserOrThrow().Returns(new CurrentUserInfo(
            1,
            "admin",
            "Admin",
            TestTenant,
            Array.Empty<string>(),
            true));

        var controller = new AppWebCozePlaygroundGatewayController(
            workspacePortalService,
            tenantProvider,
            currentUserAccessor);
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            }
        };
        return controller;
    }

    private static AppWebCozeDeveloperGatewayController BuildDeveloperController()
    {
        var workspacePortalService = Substitute.For<IWorkspacePortalService>();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenantId().Returns(TestTenant);
        var currentUserAccessor = Substitute.For<ICurrentUserAccessor>();
        currentUserAccessor.GetCurrentUserOrThrow().Returns(new CurrentUserInfo(
            1,
            "admin",
            "Admin",
            TestTenant,
            Array.Empty<string>(),
            true));

        var controller = new AppWebCozeDeveloperGatewayController(
            workspacePortalService,
            tenantProvider,
            currentUserAccessor);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().BuildServiceProvider()
            }
        };
        return controller;
    }

    private static CozeIntelligenceCompatController BuildIntelligenceController(Action<IServiceCollection>? configureServices)
    {
        var workspacePortalService = Substitute.For<IWorkspacePortalService>();
        workspacePortalService.ListWorkspacesAsync(TestTenant, 1, true, Arg.Any<CancellationToken>())
            .Returns([
                new WorkspaceListItem("1", "org-1", "Workspace", null, null, null, "ws-app", "Owner", 0, 0, 0, DateTime.UtcNow.ToString("O"), null)
            ]);
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenantId().Returns(TestTenant);
        var currentUserAccessor = Substitute.For<ICurrentUserAccessor>();
        currentUserAccessor.GetCurrentUserOrThrow().Returns(new CurrentUserInfo(
            1,
            "admin",
            "Admin",
            TestTenant,
            Array.Empty<string>(),
            true));

        var controller = new CozeIntelligenceCompatController(
            workspacePortalService,
            tenantProvider,
            currentUserAccessor);
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            }
        };
        return controller;
    }
}
