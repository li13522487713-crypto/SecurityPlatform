extern alias AppHost;

using Atlas.Application.Audit.Abstractions;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Models;
using Atlas.Core.Resilience;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Services.SetupConsole;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using RefreshConsoleTokenRequest = AppHost::Atlas.AppHost.Controllers.RefreshConsoleTokenRequest;
using SetupConsoleAuthController = AppHost::Atlas.AppHost.Controllers.SetupConsoleAuthController;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// SetupConsoleAuthController IP 限流（M10/D4）+ 审计 IP/UA（M10/D5）覆盖测试。
///
/// 验证矩阵：
///   - 限流被拒：返回 429 + 错误码 RECOVERY_KEY_RATE_LIMITED + 写审计 auth.recover.rate-limited
///   - 限流通过 + 凭证错：返回 401 RECOVERY_KEY_INVALID + 写审计 auth.recover.failed
///   - 限流通过 + 凭证对：返回 200 + Token + 写审计 auth.recover.succeeded
///   - Refresh：缺 token 返 400；token 无效返 401；token 有效返 200
///   - Revoke：写审计 auth.revoke 并返回 200
/// 所有写审计的 record 都必须带上当前请求的 IP / User-Agent。
/// </summary>
public sealed class SetupConsoleAuthControllerRateLimitTests
{
    private const string TestIp = "203.0.113.7";
    private const string TestUa = "Mozilla/5.0 (PlaywrightTest)";
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task Recover_RateLimited_Returns429AndAuditsRateLimit()
    {
        var (controller, deps) = BuildController(rateLimitAllowed: false);

        var result = await controller.Recover(
            new ConsoleAuthChallengeRequest("ATLS-MOCK-AAAA-BBBB-CCCC-DDDD", null, null),
            CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, status.StatusCode);
        var payload = Assert.IsType<ApiResponse<ConsoleAuthTokenDto>>(status.Value);
        Assert.False(payload.Success);
        Assert.Equal("RECOVERY_KEY_RATE_LIMITED", payload.Code);

        // 没有进入认证服务
        await deps.Service.DidNotReceive().AuthenticateAsync(Arg.Any<ConsoleAuthChallengeRequest>(), Arg.Any<CancellationToken>());
        // 写了限流审计，IP/UA 必须落库
        await deps.Inner.Received(1).WriteAsync(Arg.Is<AuditRecord>(r =>
            r.Action == "setup-console.auth.recover.rate-limited"
            && r.IpAddress == TestIp
            && r.UserAgent == TestUa), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recover_InvalidCredentials_Returns401AndAuditsFailure()
    {
        var (controller, deps) = BuildController(rateLimitAllowed: true);
        deps.Service.AuthenticateAsync(Arg.Any<ConsoleAuthChallengeRequest>(), Arg.Any<CancellationToken>())
            .Returns((ConsoleAuthTokenDto?)null);

        var result = await controller.Recover(
            new ConsoleAuthChallengeRequest("WRONG-KEY", null, null),
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<ConsoleAuthTokenDto>>(unauthorized.Value);
        Assert.False(payload.Success);
        Assert.Equal("RECOVERY_KEY_INVALID", payload.Code);

        await deps.Inner.Received(1).WriteAsync(Arg.Is<AuditRecord>(r =>
            r.Action == "setup-console.auth.recover.failed"
            && r.IpAddress == TestIp
            && r.UserAgent == TestUa), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recover_ValidCredentials_Returns200AndAuditsSuccess()
    {
        var (controller, deps) = BuildController(rateLimitAllowed: true);
        var token = new ConsoleAuthTokenDto(
            "console-token-xyz",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(30),
            new[] { "system", "workspace", "migration" });
        deps.Service.AuthenticateAsync(Arg.Any<ConsoleAuthChallengeRequest>(), Arg.Any<CancellationToken>())
            .Returns(token);

        var result = await controller.Recover(
            new ConsoleAuthChallengeRequest("ATLS-MOCK-AAAA-BBBB-CCCC-DDDD", null, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<ConsoleAuthTokenDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("console-token-xyz", payload.Data!.ConsoleToken);

        await deps.Inner.Received(1).WriteAsync(Arg.Is<AuditRecord>(r =>
            r.Action == "setup-console.auth.recover.succeeded"
            && r.IpAddress == TestIp
            && r.UserAgent == TestUa), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_MissingToken_Returns400()
    {
        var (controller, _) = BuildController(rateLimitAllowed: true);

        var result = await controller.Refresh(new RefreshConsoleTokenRequest(string.Empty), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<ConsoleAuthTokenDto>>(bad.Value);
        Assert.Equal("VALIDATION_ERROR", payload.Code);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var (controller, deps) = BuildController(rateLimitAllowed: true);
        deps.Service.RefreshAsync("expired-token", Arg.Any<CancellationToken>())
            .Returns((ConsoleAuthTokenDto?)null);

        var result = await controller.Refresh(new RefreshConsoleTokenRequest("expired-token"), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<ConsoleAuthTokenDto>>(unauthorized.Value);
        Assert.Equal("CONSOLE_TOKEN_EXPIRED", payload.Code);
    }

    [Fact]
    public async Task Revoke_WritesAuditAndReturns200()
    {
        var (controller, deps) = BuildController(rateLimitAllowed: true);

        var result = await controller.Revoke(new RefreshConsoleTokenRequest("token-to-kill"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        await deps.Service.Received(1).RevokeAsync("token-to-kill", Arg.Any<CancellationToken>());
        await deps.Inner.Received(1).WriteAsync(Arg.Is<AuditRecord>(r =>
            r.Action == "setup-console.auth.revoke"
            && r.IpAddress == TestIp
            && r.UserAgent == TestUa), Arg.Any<CancellationToken>());
    }

    private static (SetupConsoleAuthController Controller, ControllerDependencies Deps) BuildController(bool rateLimitAllowed)
    {
        var service = Substitute.For<ISetupRecoveryKeyService>();
        var rateLimiter = Substitute.For<IRateLimiter>();
        rateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(rateLimitAllowed);

        var inner = Substitute.For<IAuditWriter>();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenantId().Returns(TestTenant);
        var auditContext = new SetupConsoleAuditContext();
        var auditWriter = new SetupConsoleAuditWriter(inner, tenantProvider, auditContext);

        var controller = new SetupConsoleAuthController(service, rateLimiter, auditWriter);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildHttpContext()
        };

        return (controller, new ControllerDependencies(service, inner, auditContext));
    }

    private static HttpContext BuildHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(TestIp);
        ctx.Request.Headers.UserAgent = TestUa;
        return ctx;
    }

    private sealed record ControllerDependencies(
        ISetupRecoveryKeyService Service,
        IAuditWriter Inner,
        SetupConsoleAuditContext AuditContext);
}
