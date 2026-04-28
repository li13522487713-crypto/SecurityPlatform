namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgeJobType
{
    Parse = 0,
    Index = 1,
    Rebuild = 2,
    Gc = 3,
    /// <summary>v5 §35 / 计划 G2：切片为独立任务阶段，Mock 与真实管线均区分。</summary>
    Chunking = 4
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

/* ---------------------------------------------------------------------- */
/* v5 §35 / 计划 G1：按 type 拆出的子 DTO（保留 KnowledgeJobDto 作为聚合父）  */
/* 客户端可直接消费这些子类型，避免每次都做 type 分支判断。                 */
/* ---------------------------------------------------------------------- */

/// <summary>解析任务 DTO（type='parse'），承载 ParsingStrategy。</summary>
public sealed record ParseJobDto(
    long Id,
    long KnowledgeBaseId,
    long? DocumentId,
    KnowledgeJobStatus Status,
    int Progress,
    int Attempts,
    int MaxAttempts,
    DateTime EnqueuedAt,
    string? ErrorMessage = null,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null,
    ParsingStrategy? ParsingStrategy = null,
    IReadOnlyList<KnowledgeJobLogEntry>? Logs = null);

/// <summary>索引任务 DTO（type='index'），承载 ChunkingProfile / Append-Overwrite mode。</summary>
public sealed record IndexJobDto(
    long Id,
    long KnowledgeBaseId,
    long? DocumentId,
    KnowledgeJobStatus Status,
    int Progress,
    int Attempts,
    int MaxAttempts,
    DateTime EnqueuedAt,
    string? ErrorMessage = null,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null,
    ChunkingProfile? ChunkingProfile = null,
    KnowledgeIndexMode? Mode = null,
    IReadOnlyList<KnowledgeJobLogEntry>? Logs = null);

/// <summary>全量重建任务 DTO（type='rebuild'）。</summary>
public sealed record RebuildJobDto(
    long Id,
    long KnowledgeBaseId,
    KnowledgeJobStatus Status,
    int Progress,
    int Attempts,
    int MaxAttempts,
    DateTime EnqueuedAt,
    string? ErrorMessage = null,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null,
    long? ScopedDocumentId = null,
    IReadOnlyList<KnowledgeJobLogEntry>? Logs = null);

/// <summary>垃圾回收任务 DTO（type='gc'）。</summary>
public sealed record GcJobDto(
    long Id,
    long KnowledgeBaseId,
    KnowledgeJobStatus Status,
    int Progress,
    DateTime EnqueuedAt,
    string? ErrorMessage = null,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null,
    IReadOnlyList<KnowledgeJobLogEntry>? Logs = null);

/// <summary>索引写入模式（v5 §35 / 计划 G6）。</summary>
public enum KnowledgeIndexMode
{
    Append = 0,
    Overwrite = 1
}

public sealed record KnowledgeJobsListRequest(
    int PageIndex = 1,
    int PageSize = 50,
    KnowledgeJobStatus? Status = null,
    KnowledgeJobType? Type = null,
    /// <summary>v5 §39 / 计划 G8：跨 KB 列表按 spaceId 过滤；为 null 时不过滤。</summary>
    long? SpaceId = null);

public sealed record RerunParseRequest(
    long DocumentId,
    ParsingStrategy? ParsingStrategy = null);

public sealed record RebuildIndexRequest(
    long? DocumentId = null,
    KnowledgeIndexMode Mode = KnowledgeIndexMode.Overwrite);

/* ---------------------------------------------------------------------- */
/* v5 §35 / 计划 G1：REST 路径专用请求体（与 documents/{id}/parse-jobs 等对应）*/
/* ---------------------------------------------------------------------- */

/// <summary>
/// 在 <c>POST /knowledge-bases/{id}/documents/{docId}/parse-jobs</c> 触发解析重跑。
/// 等价于旧 <see cref="RerunParseRequest"/>，但不再要求路径外重复传 documentId。
/// </summary>
public sealed record ParseJobReplayRequest(
    ParsingStrategy? ParsingStrategy = null);

/// <summary>
/// 在 <c>POST /knowledge-bases/{id}/documents/{docId}/index-jobs/rebuild</c> 触发索引重建。
/// </summary>
public sealed record IndexJobRebuildRequest(
    ChunkingProfile? ChunkingProfile = null,
    KnowledgeIndexMode Mode = KnowledgeIndexMode.Overwrite);

/// <summary>
/// 死信批量重投请求（v5 §42 / 计划 G5）。
/// 留空表示重投当前 KB 全部 DeadLetter；指定 jobIds 时只重投这几条。
/// </summary>
public sealed record DeadLetterRetryRequest(
    IReadOnlyList<long>? JobIds = null,
    KnowledgeJobType? Type = null);
