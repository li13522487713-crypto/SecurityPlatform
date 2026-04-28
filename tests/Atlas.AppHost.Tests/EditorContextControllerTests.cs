using System.Text.Json;
using Atlas.AppHost.Controllers;
using Atlas.Application.Authorization;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Atlas.AppHost.Tests;

public sealed class EditorContextControllerTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Theory]
    [InlineData("app", "101")]
    [InlineData("workflow", "202")]
    [InlineData("agent", "303")]
    public async Task ResolveWorkspace_ShouldReturnWorkspaceForSupportedResources(string resourceType, string resourceId)
    {
        var workspaceLookup = Substitute.For<IResourceWorkspaceLookup>();
        workspaceLookup.ResolveWorkspaceIdAsync(TestTenant, resourceType, long.Parse(resourceId), Arg.Any<CancellationToken>())
            .Returns(2001);
        var workspacePortal = Substitute.For<IWorkspacePortalService>();
        workspacePortal.ListWorkspacesAsync(TestTenant, 1, true, Arg.Any<CancellationToken>())
            .Returns([
                new WorkspaceListItem(
                    "2001",
                    "org-1",
                    "Workspace A",
                    "desc",
                    null,
                    "app-main",
                    "atlas-app",
                    "Owner",
                    0,
                    0,
                    0,
                    DateTimeOffset.UtcNow.ToString("O"),
                    null)
            ]);

        var controller = BuildController(workspaceLookup, workspacePortal);

        var result = await controller.ResolveWorkspace(resourceType, resourceId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"success\":true", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"\"resourceType\":\"{resourceType}\"", payload, StringComparison.Ordinal);
        Assert.Contains($"\"resourceId\":\"{resourceId}\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"workspaceId\":\"2001\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveWorkspace_ShouldReturnFailureWhenWorkspaceCannotBeResolved()
    {
        var workspaceLookup = Substitute.For<IResourceWorkspaceLookup>();
        workspaceLookup.ResolveWorkspaceIdAsync(TestTenant, "workflow", 404, Arg.Any<CancellationToken>())
            .Returns((long?)null);
        var workspacePortal = Substitute.For<IWorkspacePortalService>();
        var controller = BuildController(workspaceLookup, workspacePortal);

        var result = await controller.ResolveWorkspace("workflow", "404", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"success\":false", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"EDITOR_CONTEXT_WORKSPACE_UNRESOLVED\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveWorkspace_ShouldReturnFailureWhenWorkspaceIsNotAccessible()
    {
        var workspaceLookup = Substitute.For<IResourceWorkspaceLookup>();
        workspaceLookup.ResolveWorkspaceIdAsync(TestTenant, "agent", 909, Arg.Any<CancellationToken>())
            .Returns(2009);
        var workspacePortal = Substitute.For<IWorkspacePortalService>();
        workspacePortal.ListWorkspacesAsync(TestTenant, 1, true, Arg.Any<CancellationToken>())
            .Returns([
                new WorkspaceListItem(
                    "2001",
                    "org-1",
                    "Workspace A",
                    "desc",
                    null,
                    "app-main",
                    "atlas-app",
                    "Owner",
                    0,
                    0,
                    0,
                    DateTimeOffset.UtcNow.ToString("O"),
                    null)
            ]);

        var controller = BuildController(workspaceLookup, workspacePortal);

        var result = await controller.ResolveWorkspace("agent", "909", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"success\":false", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"EDITOR_CONTEXT_WORKSPACE_FORBIDDEN\"", payload, StringComparison.Ordinal);
    }

    private static EditorContextController BuildController(
        IResourceWorkspaceLookup workspaceLookup,
        IWorkspacePortalService workspacePortalService)
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
        services.AddSingleton(workspaceLookup);
        services.AddSingleton(workspacePortalService);
        var serviceProvider = services.BuildServiceProvider();

        var controller = new EditorContextController(
            workspaceLookup,
            workspacePortalService,
            tenantProvider,
            currentUserAccessor);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            }
        };
        return controller;
    }
}
