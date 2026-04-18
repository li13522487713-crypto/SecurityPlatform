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

        // 内容安全策略（CSP）- 防御XSS和数据注入攻击。
        //
        // P2-5 修复（PLAN §M17 S17-4 + lowcode-publish-spec.md "严格 CSP" 要求）：
        // 此前 CSP 含 'unsafe-inline' 与 'unsafe-eval'，与 spec 中"严格 CSP"不符且属等保 2.0 高风险点。
        // 现修正为：
        //  - script-src：默认仅 'self'；通过 nonce 机制为运行时注入的 inline script 赋予一次性 nonce
        //    （nonce 由当前请求生成并通过 HttpContext.Items["csp-nonce"] 暴露给视图）；
        //    生产模式下严禁 unsafe-eval（即使开发模式 hot-reload 需要也走 nonce 模式）；
        //  - style-src：保留 'unsafe-inline' —— Semi UI 依赖运行时注入 style；后续将迁移到 nonce 化样式；
        //  - connect-src：扩展为 'self' https://*.atlas.local，允许调 PlatformHost / AppHost 跨子域；
        //  - frame-ancestors：保持 'none'（禁止被外站 iframe 嵌入）；
        //  - 嵌入 SDK / hosted app 的下游页面应在自己的 CSP 中允许 cdn.atlas.local 加载 atlas-lowcode SDK。
        var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        context.Items["csp-nonce"] = nonce;
        var cspPolicy = "default-src 'self'; " +
                       $"script-src 'self' 'nonce-{nonce}'; " +
                       "style-src 'self' 'unsafe-inline'; " +
                       "img-src 'self' data: https:; " +
                       "font-src 'self' data:; " +
                       "connect-src 'self' https://*.atlas.local; " +
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
