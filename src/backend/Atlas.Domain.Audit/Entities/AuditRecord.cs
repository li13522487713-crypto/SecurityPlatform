using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Audit.Entities;

public sealed class AuditRecord : TenantEntity
{
#pragma warning disable CS8618
    // SqlSugar requires a parameterless constructor for deserialization
    // The parameterized constructor should always be used for creating new audit records
    public AuditRecord()
        : base(TenantId.Empty)
    {
        Actor = string.Empty;
        Action = string.Empty;
        Result = string.Empty;
        Target = string.Empty;
        OccurredAt = DateTimeOffset.UtcNow;
    }
#pragma warning restore CS8618

    public AuditRecord(
        TenantId tenantId,
        string actor,
        string action,
        string result,
        string? target,
        string? ipAddress,
        string? userAgent)
        : base(tenantId)
    {
        Actor = actor;
        Action = action;
        Result = result;
        Target = target ?? string.Empty;
        IpAddress = ipAddress ?? string.Empty;
        UserAgent = userAgent ?? string.Empty;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public string Actor { get; private set; }
    public string Action { get; private set; }
    public string Result { get; private set; }
    public string Target { get; private set; }
    public string IpAddress { get; private set; }
    public string UserAgent { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
