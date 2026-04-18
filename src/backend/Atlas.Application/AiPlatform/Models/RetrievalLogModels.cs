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

/// <summary>
/// 调用方场景预设（v5 §38 计划 G8）：前端 retrieval-tab 的 callerContext.preset 选择器映射到此字段。
/// 后端审计日志按预设打分类，便于按场景查询召回行为。
/// </summary>
public enum RetrievalCallerPreset
{
    Assistant = 0,
    WorkflowDebug = 1,
    ExternalApi = 2,
    System = 3
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
    string? UserId = null,
    RetrievalCallerPreset? Preset = null);

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

/// <summary>升级版检索请求：与 v5 报告 §38 / 前端 RetrievalRequest 对齐（计划 G4）。</summary>
public sealed record RetrievalRequest(
    string Query,
    IReadOnlyList<long> KnowledgeBaseIds,
    int TopK,
    RetrievalCallerContext CallerContext,
    bool Debug = false,
    float? MinScore = null,
    IReadOnlyDictionary<string, string>? Filters = null,
    RetrievalProfile? RetrievalProfile = null,
    /// <summary>顶层 rerank 开关；若设置则覆盖 <see cref="RetrievalProfile.EnableRerank"/>。</summary>
    bool? Rerank = null);

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

/// <summary>
/// 检索响应：扁平化版本（计划 G4）。
/// 顶层字段直接对应前端 <c>RetrievalResponse</c> 协议；<c>Log</c> 保留为完整原始记录方便调试与日志写入。
/// </summary>
public sealed record RetrievalResponseDto(
    RetrievalLogDto Log,
    string? TraceId = null,
    string? RawQuery = null,
    string? RewrittenQuery = null,
    IReadOnlyList<RetrievalCandidate>? Candidates = null,
    IReadOnlyList<RetrievalCandidate>? Reranked = null,
    string? FinalContext = null,
    string? EmbeddingModel = null,
    string? VectorStore = null,
    int? LatencyMs = null)
{
    /// <summary>从 <see cref="RetrievalLogDto"/> 派生扁平字段，便于调用方一次构造。</summary>
    public static RetrievalResponseDto FromLog(RetrievalLogDto log) => new(
        log,
        log.TraceId,
        log.RawQuery,
        log.RewrittenQuery,
        log.Candidates,
        log.Reranked,
        log.FinalContext,
        log.EmbeddingModel,
        log.VectorStore,
        log.LatencyMs);
}

public sealed record RetrievalLogQuery(
    int PageIndex = 1,
    int PageSize = 20,
    KnowledgeRetrievalCallerType? CallerType = null,
    DateTime? FromTs = null,
    DateTime? ToTs = null);
