using System.Diagnostics;
using System.Security.Claims;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Presentation.Shared.Security;

namespace Atlas.Presentation.Shared.Middlewares;

public sealed class OpenApiGovernanceMiddleware
{
    private const string OpenApiPrefix = "/api/v1/open/";
    private readonly RequestDelegate _next;

    public OpenApiGovernanceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        OpenApiProjectRateLimiter rateLimiter,
        IOpenApiCallLogService callLogService)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith(OpenApiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var startedAtUtc = DateTime.UtcNow;
        var startTimestamp = Stopwatch.GetTimestamp();
        var user = context.User;
        var projectId = ParseLongClaim(user, OpenProjectAuthenticationHandler.ProjectIdClaim);
        var appId = user.FindFirstValue(OpenProjectAuthenticationHandler.AppIdClaim);
        var userId = ParseLongClaim(user, ClaimTypes.NameIdentifier) ?? 0L;
        var tokenType = user.FindFirstValue(OpenProjectAuthenticationHandler.TokenTypeClaim);
        var isOpenProjectToken = string.Equals(
            tokenType,
            OpenProjectAuthenticationHandler.TokenTypeValue,
            StringComparison.Ordinal);

        if (isOpenProjectToken && projectId.HasValue)
        {
            var tenantId = tenantProvider.GetTenantId();
            var rateLimitKey = $"{tenantId.Value:N}:{projectId.Value}";
            if (!rateLimiter.TryAcquire(rateLimitKey, out var retryAfterSeconds))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                var payload = ApiResponse<object>.Fail(
                    "RATE_LIMITED",
                    "开放应用请求过于频繁，请稍后重试。",
                    context.TraceIdentifier);
                await context.Response.WriteAsJsonAsync(payload);

                await TryWriteCallLogAsync(
                    context,
                    tenantProvider,
                    callLogService,
                    new OpenApiCallLogCreateRequest(
                        projectId,
                        appId,
                        userId,
                        ApiName: $"{context.Request.Method.ToUpperInvariant()} {path}",
                        HttpMethod: context.Request.Method.ToUpperInvariant(),
                        RequestPath: path,
                        IsSuccess: false,
                        StatusCode: StatusCodes.Status429TooManyRequests,
                        ErrorCode: "RATE_LIMITED",
                        DurationMs: 0,
                        TraceId: context.TraceIdentifier,
                        CreatedAt: startedAtUtc));
                return;
            }
        }

        Exception? capturedException = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            var durationMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            var statusCode = context.Response.StatusCode;
            var isSuccess = capturedException is null && statusCode is >= 200 and < 400;

            await TryWriteCallLogAsync(
                context,
                tenantProvider,
                callLogService,
                new OpenApiCallLogCreateRequest(
                    projectId,
                    appId,
                    userId,
                    ApiName: $"{context.Request.Method.ToUpperInvariant()} {path}",
                    HttpMethod: context.Request.Method.ToUpperInvariant(),
                    RequestPath: path,
                    IsSuccess: isSuccess,
                    StatusCode: statusCode,
                    ErrorCode: isSuccess ? null : statusCode.ToString(),
                    DurationMs: durationMs,
                    TraceId: context.TraceIdentifier,
                    CreatedAt: startedAtUtc));
        }
    }

    private static long? ParseLongClaim(ClaimsPrincipal principal, string claimType)
    {
        var raw = principal.FindFirstValue(claimType);
        return long.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static async Task TryWriteCallLogAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        IOpenApiCallLogService callLogService,
        OpenApiCallLogCreateRequest request)
    {
        try
        {
            var tenantId = tenantProvider.GetTenantId();
            if (tenantId.IsEmpty)
            {
                return;
            }

            await callLogService.WriteAsync(tenantId, request, context.RequestAborted);
        }
        catch
        {
            // 记录失败不影响主流程
        }
    }
}
