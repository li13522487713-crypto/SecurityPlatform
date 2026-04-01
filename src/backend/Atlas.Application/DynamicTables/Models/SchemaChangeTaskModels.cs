namespace Atlas.Application.DynamicTables.Models;

public sealed record SchemaChangeTaskListItem(
    long Id,
    long AppInstanceId,
    string CurrentState,
    bool IsHighRisk,
    string? ValidationResult,
    string? AffectedResourcesSummary,
    string? ErrorMessage,
    string? RollbackInfo,
    long Operator,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

public sealed record SchemaChangeTaskCreateRequest(
    long AppInstanceId,
    IReadOnlyList<long> DraftIds);
