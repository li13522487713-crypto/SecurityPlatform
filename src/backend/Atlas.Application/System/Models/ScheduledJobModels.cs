namespace Atlas.Application.System.Models;

public sealed record ScheduledJobDto(
    string Id,
    string Name,
    string CronExpression,
    string Queue,
    bool IsEnabled,
    DateTimeOffset? LastRunAt,
    string? LastRunStatus,
    DateTimeOffset? NextRunAt);

public sealed record ScheduledJobExecutionDto(
    string JobId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long? DurationMilliseconds,
    string? State,
    string? ErrorMessage);
