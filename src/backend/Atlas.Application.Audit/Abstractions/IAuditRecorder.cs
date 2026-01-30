using Atlas.Application.Audit.Models;

namespace Atlas.Application.Audit.Abstractions;

public interface IAuditRecorder
{
    Task RecordAsync(AuditContext context, CancellationToken cancellationToken);
}
