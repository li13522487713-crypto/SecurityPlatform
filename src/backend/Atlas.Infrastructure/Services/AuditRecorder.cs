using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Domain.Audit.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class AuditRecorder : IAuditRecorder
{
    private readonly IAuditWriter _auditWriter;

    public AuditRecorder(IAuditWriter auditWriter)
    {
        _auditWriter = auditWriter;
    }

    public Task RecordAsync(AuditContext context, CancellationToken cancellationToken)
    {
        var record = new AuditRecord(
            context.TenantId,
            context.Actor,
            context.Action,
            context.Result,
            context.Target,
            context.IpAddress,
            context.UserAgent,
            context.ClientContext.ClientType.ToString(),
            context.ClientContext.ClientPlatform.ToString(),
            context.ClientContext.ClientChannel.ToString(),
            context.ClientContext.ClientAgent.ToString());

        return _auditWriter.WriteAsync(record, cancellationToken);
    }
}
