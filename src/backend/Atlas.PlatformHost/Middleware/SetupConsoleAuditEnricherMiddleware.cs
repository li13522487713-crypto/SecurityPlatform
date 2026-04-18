using Atlas.Infrastructure.Services.SetupConsole;

namespace Atlas.PlatformHost.Middleware;

/// <summary>
/// 控制台审计 IP/UA 注入中间件（M10/D5）。
///
/// 仅对 <c>/api/v1/setup-console/*</c> 路径（含 <c>/migration/*</c> 子路径）生效：
///  - 从 <see cref="HttpContext.Connection.RemoteIpAddress"/> 解析出 IP（IPv4 / IPv6 字符串）
///  - 从 <c>User-Agent</c> 头读取 UA
///  - 写入当前请求的 <see cref="SetupConsoleAuditContext"/> Scoped 实例
///
/// 让 <see cref="SetupConsoleAuditWriter"/> 在 Service / Controller 层调用 WriteAsync 时，
/// 即使没显式传 ipAddress/userAgent，也能自动补全审计记录的客户端来源信息。
/// </summary>
public sealed class SetupConsoleAuditEnricherMiddleware
{
    private const string SetupConsolePathPrefix = "/api/v1/setup-console";

    private readonly RequestDelegate _next;

    public SetupConsoleAuditEnricherMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SetupConsoleAuditContext auditContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith(SetupConsolePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var ua = context.Request.Headers.UserAgent.ToString();
            auditContext.Capture(ip, string.IsNullOrEmpty(ua) ? null : ua);
        }

        await _next(context);
    }
}
