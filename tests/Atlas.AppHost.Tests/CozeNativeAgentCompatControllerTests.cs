using System.Text.Json;
using Atlas.AppHost.Controllers;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
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

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(queryService);
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

        var controller = BuildIntelligenceController(services =>
        {
            services.AddSingleton(queryService);
        });

        var result = await controller.GetPublishRecordList(
            new CozeGetPublishRecordListRequest("102"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"version_number\":\"v2\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"publish_record_id\":\"bot-102-v2\"", payload, StringComparison.Ordinal);
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
