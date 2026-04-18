using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 系统初始化与迁移控制台专用审计 Writer（M7）。
///
/// 把所有控制台写操作（schema/seed/bootstrap-user/default-workspace/complete/retry/reopen/migration/*）
/// 标准化为 <see cref="AuditRecord"/> 写入审计库。
///
/// 与 <c>IAuditWriter</c> 共享底层实现，但封装了 actor / action 的命名约定：
///  - actor: "setup-console:anonymous"（M7 临时；M8 起接 ConsoleSession 真实身份）
///  - action: "setup-console.&lt;step or operation&gt;"
///  - target: 任务 / 工作空间 / 实体名
///
/// 不直接依赖 <c>IHttpContextAccessor</c>（Atlas.Infrastructure 不引 ASP.NET Core）；
/// IP / UserAgent 的补全由 Controller 调用方传入 ipAddress / userAgent 参数即可。
/// </summary>
public sealed class SetupConsoleAuditWriter
{
    private const string ActorAnonymousConsole = "setup-console:anonymous";

    private readonly IAuditWriter _inner;
    private readonly ITenantProvider _tenantProvider;
    private readonly SetupConsoleAuditContext? _auditContext;

    public SetupConsoleAuditWriter(
        IAuditWriter inner,
        ITenantProvider tenantProvider,
        SetupConsoleAuditContext? auditContext = null)
    {
        _inner = inner;
        _tenantProvider = tenantProvider;
        _auditContext = auditContext;
    }

    /// <summary>
    /// M10/D5：当 ipAddress / userAgent 未由调用方显式传入时，
    /// 从 <see cref="SetupConsoleAuditContext"/> 中回退读取（由 SetupConsoleAuditEnricherMiddleware 注入）。
    /// </summary>
    public Task WriteAsync(
        string action,
        string target,
        string result = "Success",
        string? message = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedIp = !string.IsNullOrWhiteSpace(ipAddress) ? ipAddress : _auditContext?.IpAddress;
        var resolvedUa = !string.IsNullOrWhiteSpace(userAgent) ? userAgent : _auditContext?.UserAgent;
        var record = new AuditRecord(
            _tenantProvider.GetTenantId(),
            actor: ActorAnonymousConsole,
            action: $"setup-console.{action}",
            result: string.IsNullOrEmpty(message) ? result : $"{result}: {message}",
            target: target,
            ipAddress: resolvedIp,
            userAgent: resolvedUa);
        return _inner.WriteAsync(record, cancellationToken);
    }
}
