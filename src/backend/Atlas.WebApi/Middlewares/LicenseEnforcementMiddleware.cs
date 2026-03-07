using Atlas.Application.License.Abstractions;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;

namespace Atlas.WebApi.Middlewares;

/// <summary>
/// 授权证书执行中间件：当证书无效或过期时，拦截非白名单请求，返回 402 Payment Required。
/// 插入位置：TenantContextMiddleware 之后。
/// </summary>
public sealed class LicenseEnforcementMiddleware
{
    // 白名单按“精确路径 + 带尾斜杠前缀”匹配，避免误放行同前缀但不同资源的路由。
    private static readonly string[] WhitelistedExactPaths =
    [
        "/api/v1/license",
        "/api/v1/auth",
        "/api/v1/health",
        "/health",
        "/openapi",
        "/swagger"
    ];

    private static readonly string[] WhitelistedPrefixes =
    [
        "/api/v1/license/",
        "/api/v1/auth/",
        "/api/v1/health/",
        "/health/",
        "/openapi/",
        "/swagger/"
    ];

    private readonly RequestDelegate _next;

    public LicenseEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 白名单路径直接放行
        var path = context.Request.Path;
        if (IsWhitelisted(path))
        {
            await _next(context);
            return;
        }

        // 匿名端点不检查授权证书（避免拦截公共 API）
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        var licenseService = context.RequestServices.GetRequiredService<ILicenseService>();
        var status = licenseService.GetCurrentStatus();

        if (status.Status == "Active")
        {
            await _next(context);
            return;
        }

        var (code, message) = status.Status switch
        {
            "Expired" => (ErrorCodes.LicenseExpired, "授权证书已过期，请续签后再使用"),
            "Invalid" => (ErrorCodes.LicenseInvalid, "授权证书无效，请重新激活"),
            _ => (ErrorCodes.LicenseInvalid, "平台尚未激活，请上传授权证书")
        };

        context.Response.StatusCode = 402;
        context.Response.ContentType = "application/json";
        var payload = ApiResponse<object?>.Fail(code, message, context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(payload);
    }

    private static bool IsWhitelisted(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var exactPath in WhitelistedExactPaths)
        {
            if (string.Equals(value, exactPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var prefix in WhitelistedPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
