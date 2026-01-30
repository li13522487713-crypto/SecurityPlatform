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
        ClientType = string.Empty;
        ClientPlatform = string.Empty;
        ClientChannel = string.Empty;
        ClientAgent = string.Empty;
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
        : this(tenantId, actor, action, result, target, ipAddress, userAgent, null, null, null, null)
    {
    }

    public AuditRecord(
        TenantId tenantId,
        string actor,
        string action,
        string result,
        string? target,
        string? ipAddress,
        string? userAgent,
        string? clientType,
        string? clientPlatform,
        string? clientChannel,
        string? clientAgent)
        : base(tenantId)
    {
        Actor = actor;
        Action = action;
        Result = result;
        Target = target ?? string.Empty;
        IpAddress = ipAddress ?? string.Empty;
        UserAgent = userAgent ?? string.Empty;
        ClientType = clientType ?? string.Empty;
        ClientPlatform = clientPlatform ?? string.Empty;
        ClientChannel = clientChannel ?? string.Empty;
        ClientAgent = clientAgent ?? string.Empty;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public string Actor { get; private set; }
    public string Action { get; private set; }
    public string Result { get; private set; }
    public string Target { get; private set; }
    public string IpAddress { get; private set; }
    public string UserAgent { get; private set; }
    public string ClientType { get; private set; }
    public string ClientPlatform { get; private set; }
    public string ClientChannel { get; private set; }
    public string ClientAgent { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
