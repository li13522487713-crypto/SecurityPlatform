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
