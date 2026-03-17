using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Diagnostics;

namespace Atlas.WebApi.Middlewares;

public sealed class TenantContextMiddleware
{
    public const string TenantClaimType = "tenant_id";

    private readonly RequestDelegate _next;
    private readonly TenancyOptions _options;

    public TenantContextMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var path = context.Request.Path.Value ?? string.Empty;
        var skipTenantCheck = allowAnonymous
            && (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/v1/health", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/v1/license/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/v1/files/signed", StringComparison.OrdinalIgnoreCase));

        if (skipTenantCheck)
        {
            await _next(context);
            return;
        }

        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        var hasHeaderTenant = TryResolveTenantFromHeader(context, out var headerTenantId);
        var hasClaimTenant = TryResolveTenantFromClaim(context, out var claimTenantId);

        if (isAuthenticated)
        {
            if (!hasClaimTenant)
            {
                await WriteTenantErrorAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "缺少租户声明");
                return;
            }

            if (hasHeaderTenant && headerTenantId != claimTenantId)
            {
                await WriteTenantErrorAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.CrossTenantForbidden, "租户标识不一致");
                return;
            }

            context.Items[HttpContextTenantProvider.TenantContextKey] = new TenantContext(claimTenantId);
            SetTelemetryTags(context, claimTenantId);
            await _next(context);
            return;
        }

        // 即使 JWT 认证未成功（如 session 失效），JWT claim 在 Token 解码时依然填充。
        // 若 claim 中含有 tenant_id 且与 header 不符，同样需要拦截并返回 403，
        // 防止攻击者通过伪造 X-Tenant-Id 绕过租户隔离。
        if (hasClaimTenant && hasHeaderTenant && headerTenantId != claimTenantId)
        {
            await WriteTenantErrorAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.CrossTenantForbidden, "租户标识不一致");
            return;
        }

        if (!hasHeaderTenant)
        {
            await WriteTenantErrorAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, "无效或缺失租户标识");
            return;
        }

        context.Items[HttpContextTenantProvider.TenantContextKey] = new TenantContext(headerTenantId);
        SetTelemetryTags(context, headerTenantId);
        await _next(context);
    }

    private bool TryResolveTenantFromHeader(HttpContext context, out TenantId tenantId)
    {
        tenantId = TenantId.Empty;

        if (context.Request.Headers.TryGetValue(_options.HeaderName, out var headerValue))
        {
            if (Guid.TryParse(headerValue.ToString(), out var tenantGuid))
            {
                tenantId = new TenantId(tenantGuid);
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool TryResolveTenantFromClaim(HttpContext context, out TenantId tenantId)
    {
        tenantId = TenantId.Empty;
        var claim = context.User.FindFirstValue(TenantClaimType);
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var claimGuid))
        {
            tenantId = new TenantId(claimGuid);
            return true;
        }

        return false;
    }

    private static async Task WriteTenantErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = ApiResponse<object?>.Fail(code, message, context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(payload);
    }

    private static void SetTelemetryTags(HttpContext context, TenantId tenantId)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        activity.SetTag("atlas.tenant_id", tenantId.Value.ToString());
        activity.SetTag("atlas.trace_id", context.TraceIdentifier);

        var id = context.Request.RouteValues.TryGetValue("id", out var idValue) ? idValue?.ToString() : null;
        var taskId = context.Request.RouteValues.TryGetValue("taskId", out var taskIdValue) ? taskIdValue?.ToString() : null;
        var instanceId = context.Request.RouteValues.TryGetValue("instanceId", out var instanceIdValue) ? instanceIdValue?.ToString() : null;

        if (!string.IsNullOrWhiteSpace(id))
        {
            activity.SetTag("atlas.resource_id", id);
        }

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            activity.SetTag("atlas.task_id", taskId);
        }

        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            activity.SetTag("atlas.instance_id", instanceId);
        }
    }
}
