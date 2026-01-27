using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

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
                || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase));

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
                await WriteTenantErrorAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "租户标识不一致");
                return;
            }

            context.Items[HttpContextTenantProvider.TenantContextKey] = new TenantContext(claimTenantId);
            await _next(context);
            return;
        }

        if (!hasHeaderTenant)
        {
            await WriteTenantErrorAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, "无效或缺失租户标识");
            return;
        }

        context.Items[HttpContextTenantProvider.TenantContextKey] = new TenantContext(headerTenantId);
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
}
