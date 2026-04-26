using Atlas.Core.Models;
using Atlas.Core.Setup;
using System.Text.Json;

namespace Atlas.Presentation.Shared.Middlewares;

/// <summary>
/// AppHost 专用 setup 门禁中间件。
/// <list type="bullet">
///   <item>应用 setup 未完成 → 所有非白名单 API 返回 503 APP_SETUP_REQUIRED</item>
///   <item>应用 setup 已完成但运行时能力尚未完全注册 → 返回 503 APP_RESTART_REQUIRED</item>
///   <item>应用 setup 已完成且运行时已就绪 → 放行</item>
/// </list>
/// 白名单路径：/api/v1/setup、/api/v1/setup-console、/health、/internal/health。
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
        "/api/v1/setup-console",
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
        IAppSetupStateProvider appSetupStateProvider,
        AppRuntimeRegistrationMarker registrationMarker)
    {
        if (appSetupStateProvider.IsReady)
        {
            if (registrationMarker.FullyRegistered)
            {
                await _next(context);
                return;
            }

            var path2 = context.Request.Path.Value ?? string.Empty;
            if (IsAllowedPath(path2) || !path2.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json; charset=utf-8";
            var restartResponse = ApiResponse<object>.Fail(
                "APP_RESTART_REQUIRED",
                "Application setup completed but the AppHost service must be restarted to load all business services.",
                context.TraceIdentifier);
            await context.Response.WriteAsync(JsonSerializer.Serialize(restartResponse, JsonOptions));
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

        var response = ApiResponse<object>.Fail(
            "APP_SETUP_REQUIRED",
            "Application setup has not been completed. Please complete the application setup wizard.",
            context.TraceIdentifier);
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
