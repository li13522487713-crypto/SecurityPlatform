namespace Atlas.Application.AiPlatform.Models;

/// <summary>
/// 检索调用方场景：v5 §38 报告强调 caller_context 决定权限/调试视图。
/// </summary>
public enum KnowledgeRetrievalCallerType
{
    Studio = 0,
    Agent = 1,
    Workflow = 2,
    App = 3,
    Chatflow = 4
}

public enum RetrievalCandidateSource
{
    Vector = 0,
    Bm25 = 1,
    Table = 2,
    Image = 3
}

public sealed record RetrievalCallerContext(
    KnowledgeRetrievalCallerType CallerType,
    string? CallerId = null,
    string? CallerName = null,
    string? ConversationId = null,
    string? WorkflowTraceId = null,
    string? PageId = null,
    string? ComponentId = null,
    string? TenantId = null,
    string? UserId = null);

public sealed record RetrievalCandidate(
    long KnowledgeBaseId,
    long DocumentId,
    long ChunkId,
    RetrievalCandidateSource Source,
    float Score,
    string Content,
    float? RerankScore = null,
    string? DocumentName = null,
    int? StartOffset = null,
    int? EndOffset = null,
    int? RowIndex = null,
    string? ImageRef = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>升级版检索请求：与 v5 报告 §38 / 前端 RetrievalRequest 对齐。</summary>
public sealed record RetrievalRequest(
    string Query,
    IReadOnlyList<long> KnowledgeBaseIds,
    int TopK,
    RetrievalCallerContext CallerContext,
    bool Debug = false,
    float? MinScore = null,
    IReadOnlyDictionary<string, string>? Filters = null,
    RetrievalProfile? RetrievalProfile = null);

public sealed record RetrievalLogDto(
    string TraceId,
    long KnowledgeBaseId,
    string RawQuery,
    RetrievalCallerContext CallerContext,
    IReadOnlyList<RetrievalCandidate> Candidates,
    IReadOnlyList<RetrievalCandidate> Reranked,
    string FinalContext,
    string EmbeddingModel,
    string VectorStore,
    int LatencyMs,
    DateTime CreatedAt,
    string? RewrittenQuery = null,
    IReadOnlyDictionary<string, string>? Filters = null);

public sealed record RetrievalResponseDto(RetrievalLogDto Log);

public sealed record RetrievalLogQuery(
    int PageIndex = 1,
    int PageSize = 20,
    KnowledgeRetrievalCallerType? CallerType = null,
    DateTime? FromTs = null,
    DateTime? ToTs = null);
