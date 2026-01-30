using Atlas.Core.Identity;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Audit.Models;

public sealed record AuditContext(
    TenantId TenantId,
    string Actor,
    string Action,
    string Result,
    string? Target,
    string? IpAddress,
    string? UserAgent,
    ClientContext ClientContext);
