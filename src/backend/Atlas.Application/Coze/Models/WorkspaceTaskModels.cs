namespace Atlas.Application.Coze.Models;

public enum WorkspaceTaskStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

public sealed record WorkspaceTaskItemDto(
    string Id,
    string Name,
    string Type,
    WorkspaceTaskStatus Status,
    DateTimeOffset StartedAt,
    long DurationMs,
    string OwnerDisplayName);

public sealed record WorkspaceTaskLogEntryDto(
    DateTimeOffset Timestamp,
    string Level,
    string Message);

public sealed record WorkspaceTaskDetailDto(
    string Id,
    string Name,
    string Type,
    WorkspaceTaskStatus Status,
    DateTimeOffset StartedAt,
    long DurationMs,
    string OwnerDisplayName,
    string? InputJson,
    string? OutputJson,
    string? ErrorMessage,
    IReadOnlyList<WorkspaceTaskLogEntryDto> Logs);
