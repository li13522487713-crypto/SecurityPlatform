using System.Security.Claims;
using Atlas.AppHost.Microflows.Infrastructure;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowProductionGuardFilterTests
{
    [Fact]
    public async Task ProductionRejectsUnauthenticatedNonHealthRequest()
    {
        var filter = CreateFilter(production: true, workspaceId: "1496769226340306944");
        var context = CreateContext(authenticated: false);

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task ProductionRequiresWorkspaceId()
    {
        var filter = CreateFilter(production: true, workspaceId: null);
        var context = CreateContext(authenticated: true);

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Theory]
    [InlineData("Microflow:Adapter:Mode", "mock")]
    [InlineData("Microflow:Adapter:Mode", "local")]
    [InlineData("Microflow:Metadata:SeedEnabled", "true")]
    [InlineData("Microflows:SeedData:Enabled", "true")]
    [InlineData("Microflow:Runtime:Rest:AllowRealHttp", "true")]
    [InlineData("Microflow:Runtime:Rest:AllowPrivateNetwork", "true")]
    [InlineData("Microflow:Diagnostics:EnableInternalDebugApi", "true")]
    public async Task ProductionRejectsUnsafeConfiguration(string key, string value)
    {
        var filter = CreateFilter(
            production: true,
            workspaceId: "1496769226340306944",
            overrides: new Dictionary<string, string?> { [key] = value });
        var context = CreateContext(authenticated: true);

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public async Task DevelopmentBypassesProductionGuard()
    {
        var filter = CreateFilter(
            production: false,
            workspaceId: null,
            overrides: new Dictionary<string, string?> { ["Microflow:Adapter:Mode"] = "mock" });
        var context = CreateContext(authenticated: false);

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    private static MicroflowProductionGuardFilter CreateFilter(
        bool production,
        string? workspaceId,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(production ? "Production" : "Development");
        var configurationValues = new Dictionary<string, string?>
        {
            ["Microflow:Security:EnableProductionGuard"] = "true",
            ["Microflow:Security:RequireWorkspaceId"] = "true",
            ["Microflow:Adapter:Mode"] = "http",
            ["Microflow:Metadata:SeedEnabled"] = "false",
            ["Microflow:Metadata:ForceSeed"] = "false",
            ["Microflows:Metadata:SeedEnabled"] = "false",
            ["Microflows:Metadata:ForceSeed"] = "false",
            ["Microflows:SeedData:Enabled"] = "false",
            ["Microflow:Runtime:Rest:AllowRealHttp"] = "false",
            ["Microflow:Runtime:Rest:AllowPrivateNetwork"] = "false",
            ["Microflow:Diagnostics:EnableInternalDebugApi"] = "false",
            ["Microflow:Diagnostics:EnableVerboseTrace"] = "false"
        };
        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                configurationValues[pair.Key] = pair.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = workspaceId,
            TraceId = "trace-production-guard"
        });
        return new MicroflowProductionGuardFilter(environment, configuration, accessor);
    }

    private static AuthorizationFilterContext CreateContext(bool authenticated)
    {
        var httpContext = new DefaultHttpContext();
        if (authenticated)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "42")],
                authenticationType: "Test"));
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }
}
