using System.Text.Json;
using Atlas.AppHost.Controllers;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Atlas.AppHost.Tests;

public sealed class WorkspacePublishChannelsControllerTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task List_ShouldReturnWorkspaceChannels()
    {
        var service = Substitute.For<IWorkspacePublishChannelService>();
        service.ListAsync(TestTenant, "ws-1", null, Arg.Any<PagedRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<WorkspacePublishChannelDto>(
                [
                    new WorkspacePublishChannelDto(
                        "channel-1",
                        "ws-1",
                        "Web SDK",
                        "web-sdk",
                        "active",
                        "authorized",
                        "desc",
                        ["agent"],
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.AddDays(-1))
                ],
                1,
                1,
                50));

        var controller = BuildController(services =>
        {
            services.AddSingleton(service);
            services.AddSingleton(Substitute.For<IWorkspaceChannelReleaseService>());
        });

        var result = await controller.List("ws-1", null, 1, 50, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"channel-1\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"Web SDK\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reauthorize_ShouldForwardRequest()
    {
        var service = Substitute.For<IWorkspacePublishChannelService>();
        var controller = BuildController(services =>
        {
            services.AddSingleton(service);
            services.AddSingleton(Substitute.For<IWorkspaceChannelReleaseService>());
        });

        var result = await controller.Reauthorize("ws-1", "channel-1", CancellationToken.None);

        await service.Received(1).ReauthorizeAsync(TestTenant, "ws-1", "channel-1", Arg.Any<CancellationToken>());
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"Success\":true", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_ShouldForwardRequest()
    {
        var service = Substitute.For<IWorkspacePublishChannelService>();
        var controller = BuildController(services =>
        {
            services.AddSingleton(service);
            services.AddSingleton(Substitute.For<IWorkspaceChannelReleaseService>());
        });

        var result = await controller.Delete("ws-1", "channel-2", CancellationToken.None);

        await service.Received(1).DeleteAsync(TestTenant, "ws-1", "channel-2", Arg.Any<CancellationToken>());
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"Success\":true", payload, StringComparison.Ordinal);
    }

    private static WorkspacePublishChannelsController BuildController(Action<IServiceCollection> configureServices)
    {
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

        var services = new ServiceCollection();
        configureServices(services);

        var controller = new WorkspacePublishChannelsController(
            services.BuildServiceProvider().GetRequiredService<IWorkspacePublishChannelService>(),
            services.BuildServiceProvider().GetRequiredService<IWorkspaceChannelReleaseService>(),
            tenantProvider,
            currentUserAccessor);
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
