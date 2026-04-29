using System.Security.Claims;
using Atlas.AppHost.Microflows.Infrastructure;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Microflows.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowWorkspaceOwnershipFilterTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task UsesQueryWorkspaceIdBeforeRequestContext()
    {
        var fixture = CreateFixture(contextWorkspaceId: "222");
        fixture.WorkspacePortal
            .GetWorkspaceAsync(Tenant, 111, 42, false, Arg.Any<CancellationToken>())
            .Returns(Workspace("111"));
        var context = CreateAuthorizationContext(queryWorkspaceId: "111");

        await fixture.Filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        await fixture.WorkspacePortal.Received(1)
            .GetWorkspaceAsync(Tenant, 111, 42, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UsesRequestContextWorkspaceIdWhenQueryMissing()
    {
        var fixture = CreateFixture(contextWorkspaceId: "222");
        fixture.WorkspacePortal
            .GetWorkspaceAsync(Tenant, 222, 42, false, Arg.Any<CancellationToken>())
            .Returns(Workspace("222"));
        var context = CreateAuthorizationContext();

        await fixture.Filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        await fixture.WorkspacePortal.Received(1)
            .GetWorkspaceAsync(Tenant, 222, 42, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolvesWorkspaceFromRouteResourceId()
    {
        var fixture = CreateFixture(contextWorkspaceId: null);
        fixture.Resources.GetByIdAsync("mf-1", Arg.Any<CancellationToken>()).Returns(new MicroflowResourceEntity
        {
            Id = "mf-1",
            WorkspaceId = "333",
            TenantId = Tenant.ToString()
        });
        fixture.WorkspacePortal
            .GetWorkspaceAsync(Tenant, 333, 42, false, Arg.Any<CancellationToken>())
            .Returns(Workspace("333"));
        var context = CreateAuthorizationContext(routeValues: new RouteValueDictionary { ["id"] = "mf-1" });

        await fixture.Filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        await fixture.Resources.Received(1).GetByIdAsync("mf-1", Arg.Any<CancellationToken>());
        await fixture.WorkspacePortal.Received(1)
            .GetWorkspaceAsync(Tenant, 333, 42, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolvesWorkspaceFromRouteAppId()
    {
        var fixture = CreateFixture(contextWorkspaceId: null);
        fixture.WorkspacePortal
            .GetWorkspaceByAppKeyAsync(Tenant, "app-1", 42, false, Arg.Any<CancellationToken>())
            .Returns(Workspace("444", appKey: "app-1"));
        fixture.WorkspacePortal
            .GetWorkspaceAsync(Tenant, 444, 42, false, Arg.Any<CancellationToken>())
            .Returns(Workspace("444", appKey: "app-1"));
        var context = CreateAuthorizationContext(routeValues: new RouteValueDictionary { ["appId"] = "app-1" });

        await fixture.Filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        await fixture.WorkspacePortal.Received(1)
            .GetWorkspaceByAppKeyAsync(Tenant, "app-1", 42, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectsWhenWorkspaceCannotBeResolved()
    {
        var fixture = CreateFixture(contextWorkspaceId: null);
        var context = CreateAuthorizationContext();

        await fixture.Filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    private static Fixture CreateFixture(string? contextWorkspaceId)
    {
        var requestContextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        requestContextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = contextWorkspaceId,
            TenantId = Tenant.ToString(),
            UserId = "42",
            UserName = "tester",
            TraceId = "trace-workspace-ownership"
        });
        var resources = Substitute.For<IMicroflowResourceRepository>();
        var workspacePortal = Substitute.For<IWorkspacePortalService>();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenantId().Returns(Tenant);
        var currentUserAccessor = Substitute.For<ICurrentUserAccessor>();
        currentUserAccessor.GetCurrentUser().Returns(User());
        currentUserAccessor.GetCurrentUserOrThrow().Returns(User());
        var filter = new MicroflowWorkspaceOwnershipFilter(
            requestContextAccessor,
            resources,
            workspacePortal,
            tenantProvider,
            currentUserAccessor);
        return new Fixture(filter, resources, workspacePortal);
    }

    private static AuthorizationFilterContext CreateAuthorizationContext(
        string? queryWorkspaceId = null,
        RouteValueDictionary? routeValues = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "42")],
                authenticationType: "Test"))
        };
        if (!string.IsNullOrWhiteSpace(queryWorkspaceId))
        {
            httpContext.Request.QueryString = new QueryString($"?workspaceId={queryWorkspaceId}");
        }

        var routeData = new RouteData();
        foreach (var pair in routeValues ?? new RouteValueDictionary())
        {
            routeData.Values[pair.Key] = pair.Value;
        }

        var actionContext = new ActionContext(
            httpContext,
            routeData,
            new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static CurrentUserInfo User()
        => new(
            42,
            "tester",
            "Tester",
            Tenant,
            Array.Empty<string>());

    private static WorkspaceDetailDto Workspace(string id, string? appKey = null)
        => new(
            id,
            Tenant.ToString(),
            $"Workspace {id}",
            null,
            null,
            appKey is null ? null : $"app-instance-{appKey}",
            appKey,
            "owner",
            ["microflow.view", "microflow.edit"],
            "2026-04-29T00:00:00Z",
            null);

    private sealed record Fixture(
        MicroflowWorkspaceOwnershipFilter Filter,
        IMicroflowResourceRepository Resources,
        IWorkspacePortalService WorkspacePortal);
}
