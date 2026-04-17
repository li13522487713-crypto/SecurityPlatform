using Atlas.Core.Models;
using Atlas.Core.Setup;
using System.Text.Json;

namespace Atlas.Presentation.Shared.Middlewares;

/// <summary>
/// Setup Mode 门禁中间件：当安装未完成时，仅放行白名单路径（setup API、健康检查、静态资源），
/// 其余请求一律返回 503 SETUP_REQUIRED。
/// 注册位置应在 ExceptionHandling 之后、Authentication 之前。
/// </summary>
public sealed class SetupModeMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] AllowedPathPrefixes =
    [
        "/api/v1/setup",
        // 系统初始化与迁移控制台（M8 显式声明；虽然 startsWith /api/v1/setup 已隐式覆盖，
        // 显式声明可防止后续把白名单收紧到 /api/v1/setup/）。
        "/api/v1/setup-console",
        "/health",
        "/internal/health"
    ];

    private readonly RequestDelegate _next;

    public SetupModeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISetupStateProvider setupStateProvider,
        PlatformRuntimeRegistrationMarker registrationMarker)
    {
        if (setupStateProvider.IsReady)
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
                "PLATFORM_RESTART_REQUIRED",
                "Platform setup completed but the PlatformHost service must be restarted to load all business services.",
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

        // 非 API 请求（可能是前端 SPA 静态资源）放行，让 SPA 自行处理
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = ApiResponse<object>.Fail(
            "SETUP_REQUIRED",
            "Platform setup has not been completed. Please complete the setup wizard first.",
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
