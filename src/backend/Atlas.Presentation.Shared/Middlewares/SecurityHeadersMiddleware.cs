namespace Atlas.Presentation.Shared.Middlewares;

/// <summary>
/// 安全HTTP头中间件 - 添加安全响应头防御常见Web攻击
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 防止MIME类型嗅探攻击
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // 防止点击劫持攻击（Clickjacking）
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // XSS过滤器（旧版浏览器兼容）
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // 内容安全策略（CSP）- 防御XSS和数据注入攻击
        // 注意：根据实际需求调整CSP策略
        var cspPolicy = "default-src 'self'; " +
                       "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // 部分宿主/动态页面需 unsafe-inline 与 unsafe-eval（生产环境建议按需收紧）
                       "style-src 'self' 'unsafe-inline'; " +
                       "img-src 'self' data: https:; " +
                       "font-src 'self' data:; " +
                       "connect-src 'self'; " +
                       "frame-ancestors 'none'; " +
                       "base-uri 'self'; " +
                       "form-action 'self';";
        context.Response.Headers.Append("Content-Security-Policy", cspPolicy);

        // Referrer策略 - 控制Referer头信息泄露
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // 权限策略（Permissions Policy）- 限制浏览器功能访问
        var permissionsPolicy = "geolocation=(), microphone=(), camera=(), payment=()";
        context.Response.Headers.Append("Permissions-Policy", permissionsPolicy);

        // 移除暴露服务器信息的响应头
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");
        context.Response.Headers.Remove("X-AspNetMvc-Version");

        await _next(context);
    }
}

/// <summary>
/// SecurityHeadersMiddleware 扩展方法
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// 添加安全HTTP头中间件
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
