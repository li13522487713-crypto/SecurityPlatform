namespace Atlas.Application.Audit.Models;

public sealed record AuditListItem(
    string Id,
    string Actor,
    string Action,
    string Result,
    string Target,
    string? IpAddress,
    string? UserAgent,
    string? ClientType,
    string? ClientPlatform,
    string? ClientChannel,
    string? ClientAgent,
    DateTimeOffset OccurredAt);
