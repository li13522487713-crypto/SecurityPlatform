using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Models;
using Atlas.Core.Resilience;
using Atlas.Infrastructure.Services.SetupConsole;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 系统初始化与迁移控制台 - 二次认证。
///
/// - 永久免登录（[AllowAnonymous]），由 SetupConsoleAuthMiddleware 在 setup 已完成的运行时仍放行；
/// - 校验恢复密钥或 BootstrapAdmin 凭证；颁发 30 分钟过期的 ConsoleToken；
/// - 客户端在控制台所有写请求中带 Header `X-Setup-Console-Token`；
/// - M10/D4 IP 限流 + D5 审计 IP/UA：失败超过 5 次/15 分钟 → 429；每次尝试均写审计。
/// </summary>
[ApiController]
[Route("api/v1/setup-console/auth")]
[AllowAnonymous]
public sealed class SetupConsoleAuthController : ControllerBase
{
    public const string ConsoleTokenHeaderName = "X-Setup-Console-Token";

    private readonly ISetupRecoveryKeyService _service;
    private readonly IRateLimiter _rateLimiter;
    private readonly SetupConsoleAuditWriter _auditWriter;

    public SetupConsoleAuthController(
        ISetupRecoveryKeyService service,
        IRateLimiter rateLimiter,
        SetupConsoleAuditWriter auditWriter)
    {
        _service = service;
        _rateLimiter = rateLimiter;
        _auditWriter = auditWriter;
    }

    [HttpPost("recover")]
    public async Task<ActionResult<ApiResponse<ConsoleAuthTokenDto>>> Recover(
        [FromBody] ConsoleAuthChallengeRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        var resourceKey = $"setup-console.recover:{ipAddress}";

        // M10/D4：IP 限流（5 次/15 分钟由 IRateLimiter 实现策略默认）
        var allowed = await _rateLimiter.TryAcquireAsync(resourceKey, cancellationToken);
        if (!allowed)
        {
            await _auditWriter.WriteAsync(
                action: "auth.recover.rate-limited",
                target: $"ip:{ipAddress}",
                result: "Failed",
                message: "rate limit exceeded",
                ipAddress: ipAddress,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<ConsoleAuthTokenDto>.Fail(
                "RECOVERY_KEY_RATE_LIMITED",
                "too many recovery attempts; please try again later",
                HttpContext.TraceIdentifier));
        }

        var token = await _service.AuthenticateAsync(request, cancellationToken);
        if (token is null)
        {
            await _auditWriter.WriteAsync(
                action: "auth.recover.failed",
                target: $"ip:{ipAddress}",
                result: "Failed",
                message: "invalid recovery key or bootstrap credentials",
                ipAddress: ipAddress,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
            return Unauthorized(ApiResponse<ConsoleAuthTokenDto>.Fail(
                "RECOVERY_KEY_INVALID",
                "recovery key or bootstrap credentials invalid",
                HttpContext.TraceIdentifier));
        }

        await _auditWriter.WriteAsync(
            action: "auth.recover.succeeded",
            target: $"ip:{ipAddress}",
            ipAddress: ipAddress,
            userAgent: userAgent,
            cancellationToken: cancellationToken);
        return Ok(ApiResponse<ConsoleAuthTokenDto>.Ok(token, HttpContext.TraceIdentifier));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<ConsoleAuthTokenDto>>> Refresh(
        [FromBody] RefreshConsoleTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConsoleToken))
        {
            return BadRequest(ApiResponse<ConsoleAuthTokenDto>.Fail(
                "VALIDATION_ERROR",
                "consoleToken is required",
                HttpContext.TraceIdentifier));
        }

        var token = await _service.RefreshAsync(request.ConsoleToken, cancellationToken);
        if (token is null)
        {
            return Unauthorized(ApiResponse<ConsoleAuthTokenDto>.Fail(
                "CONSOLE_TOKEN_EXPIRED",
                "console token expired or invalid",
                HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<ConsoleAuthTokenDto>.Ok(token, HttpContext.TraceIdentifier));
    }

    [HttpPost("revoke")]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(
        [FromBody] RefreshConsoleTokenRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        if (!string.IsNullOrWhiteSpace(request.ConsoleToken))
        {
            await _service.RevokeAsync(request.ConsoleToken, cancellationToken);
            await _auditWriter.WriteAsync(
                action: "auth.revoke",
                target: $"ip:{ipAddress}",
                ipAddress: ipAddress,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
        }
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}

public sealed record RefreshConsoleTokenRequest(string ConsoleToken);
