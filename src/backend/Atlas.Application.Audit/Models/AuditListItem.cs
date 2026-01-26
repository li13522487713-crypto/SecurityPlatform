namespace Atlas.Application.Audit.Models;

public sealed record AuditListItem(Guid Id, string Action, DateTimeOffset OccurredAt);