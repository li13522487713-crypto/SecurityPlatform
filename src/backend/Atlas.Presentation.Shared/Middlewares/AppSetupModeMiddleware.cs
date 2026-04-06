using Atlas.Core.Models;
using Atlas.Core.Setup;
using System.Text.Json;

namespace Atlas.Presentation.Shared.Middlewares;

/// <summary>
/// AppHost 专用两级 setup 门禁中间件。
/// <list type="bullet">
///   <item>平台 setup 未完成 → 所有非白名单 API 返回 503 PLATFORM_SETUP_REQUIRED</item>
///   <item>平台已 Ready 但应用 setup 未完成 → 所有非白名单 API 返回 503 APP_SETUP_REQUIRED</item>
///   <item>两级均 Ready → 放行</item>
/// </list>
/// 白名单路径：/api/v1/setup、/health、/internal/health。
/// 注册位置应在 ExceptionHandling 之后、Authentication 之前。
/// </summary>
public sealed class AppSetupModeMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] AllowedPathPrefixes =
    [
        "/api/v1/setup",
        "/health",
        "/internal/health"
    ];

    private readonly RequestDelegate _next;

    public AppSetupModeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISetupStateProvider platformSetupStateProvider,
        IAppSetupStateProvider appSetupStateProvider)
    {
        if (platformSetupStateProvider.IsReady && appSetupStateProvider.IsReady)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        if (IsAllowedPath(path))
        {
            await _next(context);
            return;
        }

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json; charset=utf-8";

        var (code, message) = !platformSetupStateProvider.IsReady
            ? ("PLATFORM_SETUP_REQUIRED", "Platform setup has not been completed. Please complete the platform setup wizard first.")
            : ("APP_SETUP_REQUIRED", "Application setup has not been completed. Please complete the application setup wizard.");

        var response = ApiResponse<object>.Fail(code, message, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static bool IsAllowedPath(string path)
    {
        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
