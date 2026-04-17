namespace Atlas.Application.LowCode.Models;

public sealed record TriggerInfoDto(
    string Id,
    string Name,
    string Kind,
    string? Cron,
    string? EventName,
    string? WorkflowId,
    string? ChatflowId,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastFiredAt);

public sealed record TriggerUpsertRequest(
    string? Id,
    string Name,
    string Kind,
    string? Cron,
    string? EventName,
    string? WorkflowId,
    string? ChatflowId,
    bool? Enabled);

public sealed record WebviewDomainInfoDto(
    string Id,
    string Domain,
    bool Verified,
    string VerificationKind,
    string VerificationToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset? VerifiedAt);

public sealed record AddWebviewDomainRequest(
    string Domain,
    string VerificationKind);
