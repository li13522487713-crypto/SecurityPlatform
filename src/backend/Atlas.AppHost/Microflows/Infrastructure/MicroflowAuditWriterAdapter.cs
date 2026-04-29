using System.Text.Json;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Microflows.Audit;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;

namespace Atlas.AppHost.Microflows.Infrastructure;

/// <summary>
/// P0-9: 把 Microflow Application 层抽象的 <see cref="IMicroflowAuditWriter"/> 适配到平台
/// 通用的 <see cref="IAuditWriter"/>。Application.Microflows 不直接依赖 Audit bounded context；
/// AppHost 注册本 adapter 把 <see cref="MicroflowAuditEvent"/> 转换为 <see cref="AuditRecord"/>。
///
/// 异常吞掉 + structured log，避免审计写失败破坏主业务路径。
/// </summary>
public sealed class MicroflowAuditWriterAdapter : IMicroflowAuditWriter
{
    private readonly IAuditWriter _auditWriter;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<MicroflowAuditWriterAdapter> _logger;

    public MicroflowAuditWriterAdapter(
        IAuditWriter auditWriter,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MicroflowAuditWriterAdapter> logger)
    {
        _auditWriter = auditWriter;
        _requestContextAccessor = requestContextAccessor;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task WriteAsync(MicroflowAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        try
        {
            var ctx = _requestContextAccessor.Current;
            var actor = ctx.UserName ?? ctx.UserId ?? "system";
            var ip = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var ua = _httpContextAccessor?.HttpContext?.Request.Headers.UserAgent.ToString();

            var tenantId = TryParseTenant(ctx.TenantId);
            var record = new AuditRecord(
                tenantId,
                actor,
                auditEvent.Action,
                auditEvent.Result,
                BuildTarget(auditEvent),
                ip,
                ua,
                clientType: "microflow",
                clientPlatform: "atlas",
                clientChannel: "api",
                clientAgent: ua);
            record.WithResource("microflow", auditEvent.ResourceId);

            await _auditWriter.WriteAsync(record, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "MicroflowAuditWriterAdapter failed to persist audit event {Action} resource={ResourceId}",
                auditEvent.Action,
                auditEvent.ResourceId);
        }
    }

    private static TenantId TryParseTenant(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TenantId.Empty;
        }
        if (Guid.TryParse(raw, out var guid))
        {
            return new TenantId(guid);
        }
        // raw 是非 GUID 字符串（例如长 ID 编码），落 Empty 但保留 details 中的标识。
        return TenantId.Empty;
    }

    private static string BuildTarget(MicroflowAuditEvent auditEvent)
    {
        var detailsJson = auditEvent.Details is null
            ? null
            : JsonSerializer.Serialize(auditEvent.Details);
        var parts = new List<string?>
        {
            auditEvent.Target,
            auditEvent.ResourceName,
            auditEvent.WorkspaceId is null ? null : $"workspace={auditEvent.WorkspaceId}",
            auditEvent.ErrorCode is null ? null : $"errorCode={auditEvent.ErrorCode}",
            detailsJson
        };
        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Cast<string>());
    }
}
