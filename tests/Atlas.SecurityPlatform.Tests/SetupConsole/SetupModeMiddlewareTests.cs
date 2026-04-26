using System.Text;
using Atlas.Core.Setup;
using Atlas.Presentation.Shared.Middlewares;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// SetupModeMiddleware A1 修复验证（M8）：
///
/// 设置未完成时必须放行：
///   - /api/v1/setup（旧 SetupController）
///   - /api/v1/setup-console/*（M5 新 SetupConsoleController + M6 DataMigrationController）
/// 其余 /api/* 一律 503。
/// </summary>
public sealed class SetupModeMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/setup/state")]
    [InlineData("/api/v1/setup/initialize")]
    [InlineData("/api/v1/setup-console/auth/recover")]
    [InlineData("/api/v1/setup-console/system/precheck")]
    [InlineData("/api/v1/setup-console/migration/jobs")]
    [InlineData("/health")]
    [InlineData("/internal/health")]
    public async Task NotReady_AllowedPath_PassesThrough(string path)
    {
        var middleware = BuildMiddleware(out var nextCalled);
        var context = BuildContext(path);
        var provider = Substitute.For<ISetupStateProvider>();
        provider.IsReady.Returns(false);

        await middleware.InvokeAsync(context, provider);

        Assert.True(nextCalled.Value);
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/workflows")]
    [InlineData("/api/v1/something-else")]
    public async Task NotReady_BlockedApiPath_Returns503SetupRequired(string path)
    {
        var middleware = BuildMiddleware(out var nextCalled);
        var context = BuildContext(path);
        var provider = Substitute.For<ISetupStateProvider>();
        provider.IsReady.Returns(false);

        await middleware.InvokeAsync(context, provider);

        Assert.False(nextCalled.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        var body = ReadBody(context);
        Assert.Contains("SETUP_REQUIRED", body);
    }

    [Fact]
    public async Task NotReady_NonApiPath_PassesThroughForSpa()
    {
        var middleware = BuildMiddleware(out var nextCalled);
        var context = BuildContext("/setup-console");
        var provider = Substitute.For<ISetupStateProvider>();
        provider.IsReady.Returns(false);

        await middleware.InvokeAsync(context, provider);

        Assert.True(nextCalled.Value); // SPA 静态资源放行，由前端自行处理
    }

    [Fact]
    public async Task Ready_FullyRegistered_PassesThroughForAnyPath()
    {
        var middleware = BuildMiddleware(out var nextCalled);
        var context = BuildContext("/api/v1/users");
        var provider = Substitute.For<ISetupStateProvider>();
        provider.IsReady.Returns(true);

        await middleware.InvokeAsync(context, provider);

        Assert.True(nextCalled.Value);
    }

    [Fact]
    public async Task Ready_PassesThroughForSetupConsolePath()
    {
        var middleware = BuildMiddleware(out var nextCalled);
        var context = BuildContext("/api/v1/setup-console/overview");
        var provider = Substitute.For<ISetupStateProvider>();
        provider.IsReady.Returns(true);

        await middleware.InvokeAsync(context, provider);

        Assert.True(nextCalled.Value);
    }

    private static SetupModeMiddleware BuildMiddleware(out NextCallSpy spy)
    {
        var localSpy = new NextCallSpy();
        spy = localSpy;
        var middleware = new SetupModeMiddleware(_ =>
        {
            localSpy.Value = true;
            return Task.CompletedTask;
        });
        return middleware;
    }

    private static HttpContext BuildContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private sealed class NextCallSpy
    {
        public bool Value { get; set; }
    }
}
