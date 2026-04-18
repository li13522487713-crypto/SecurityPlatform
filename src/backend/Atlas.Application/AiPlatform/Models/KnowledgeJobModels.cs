namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgeJobType
{
    Parse = 0,
    Index = 1,
    Rebuild = 2,
    Gc = 3
}

public enum KnowledgeJobStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Retrying = 4,
    DeadLetter = 5,
    Canceled = 6
}

public sealed record KnowledgeJobLogEntry(
    DateTime Timestamp,
    string Level,
    string Message);

public sealed record KnowledgeJobDto(
    long Id,
    long KnowledgeBaseId,
    KnowledgeJobType Type,
    KnowledgeJobStatus Status,
    int Progress,
    int Attempts,
    int MaxAttempts,
    DateTime EnqueuedAt,
    long? DocumentId = null,
    string? ErrorMessage = null,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null,
    string? PayloadJson = null,
    IReadOnlyList<KnowledgeJobLogEntry>? Logs = null);

public sealed record KnowledgeJobsListRequest(
    int PageIndex = 1,
    int PageSize = 50,
    KnowledgeJobStatus? Status = null,
    KnowledgeJobType? Type = null);

public sealed record RerunParseRequest(
    long DocumentId,
    ParsingStrategy? ParsingStrategy = null);

public sealed record RebuildIndexRequest(
    long? DocumentId = null);
