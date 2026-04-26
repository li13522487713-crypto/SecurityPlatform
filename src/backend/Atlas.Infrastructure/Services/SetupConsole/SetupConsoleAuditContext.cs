namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 控制台审计上下文（M10/D5）。
///
/// 跨 Controller / 服务/ Writer 共享当前请求的客户端 IP 与 UA，
/// 由 <see cref="Atlas.AppHost.Middleware.SetupConsoleAuditEnricherMiddleware"/> 在请求进入控制台路径时写入；
/// 由 <see cref="SetupConsoleAuditWriter"/> 在调用方未显式传 ipAddress / userAgent 时回退读取。
///
/// 用普通可变 POCO 表达 per-request scoped 状态：
///   - DI 注册为 Scoped（<see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped"/>）；
///   - 每次请求拿到独立实例；
///   - 避免在 Atlas.Infrastructure 直接依赖 ASP.NET Core <c>HttpContext</c>，保持分层洁净。
/// </summary>
public sealed class SetupConsoleAuditContext
{
    /// <summary>客户端 IP（IPv4 / IPv6 字符串），未知时为 null。</summary>
    public string? IpAddress { get; private set; }

    /// <summary>User-Agent 原始字符串，未知时为 null。</summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// 由请求 enricher 调用，写入当前请求的客户端环境信息。
    ///
    /// 多次调用时以最后一次为准（middleware 链中可叠加补全）。
    /// </summary>
    public void Capture(string? ipAddress, string? userAgent)
    {
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            IpAddress = ipAddress;
        }
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            UserAgent = userAgent;
        }
    }
}
