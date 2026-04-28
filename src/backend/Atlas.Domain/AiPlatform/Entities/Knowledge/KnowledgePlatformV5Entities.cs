using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities.Knowledge;

/* ====================================================================== */
/* v5 §40 / 计划 G2：KnowledgeDocumentVersion                              */
/* ====================================================================== */

/// <summary>
/// 知识库版本快照（v5 §40 / 计划 G2 重命名）。
/// snapshot_ref 指向已固化的 Schema 元信息文件 / 行；回退仅恢复 Schema，已上传文件保留。
/// 旧名 <see cref="KnowledgeVersionEntity"/> 仍以别名形式保留，已标记 <see cref="ObsoleteAttribute"/>。
/// </summary>
[SugarTable("knowledge_document_version")]
public sealed class KnowledgeDocumentVersion : TenantEntity
{
    public KnowledgeDocumentVersion()
        : base(TenantId.Empty)
    {
        Label = string.Empty;
        SnapshotRef = string.Empty;
        CreatedBy = string.Empty;
        Status = "draft";
        CreatedAt = DateTime.UtcNow;
    }

    public KnowledgeDocumentVersion(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        string label,
        string? note,
        string snapshotRef,
        int documentCount,
        int chunkCount,
        string createdBy,
        string status = "draft")
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        Label = label;
        Note = note;
        SnapshotRef = snapshotRef;
        DocumentCount = documentCount;
        ChunkCount = chunkCount;
        CreatedBy = createdBy;
        Status = string.IsNullOrWhiteSpace(status) ? "draft" : status;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "label", Length = 64)]
    public string Label { get; private set; }

    [SugarColumn(ColumnName = "note", IsNullable = true, Length = 2000)]
    public string? Note { get; private set; }

    [SugarColumn(ColumnName = "snapshot_ref", Length = 256)]
    public string SnapshotRef { get; private set; }

    [SugarColumn(ColumnName = "document_count")]
    public int DocumentCount { get; private set; }

    [SugarColumn(ColumnName = "chunk_count")]
    public int ChunkCount { get; private set; }

    [SugarColumn(ColumnName = "created_by", Length = 128)]
    public string CreatedBy { get; private set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; private set; }

    [SugarColumn(ColumnName = "released_at", IsNullable = true)]
    public DateTime? ReleasedAt { get; private set; }

    [SugarColumn(ColumnName = "status", Length = 32)]
    public string Status { get; private set; }

    public void Release(DateTime now)
    {
        Status = "released";
        ReleasedAt = now;
    }

    public void Archive()
    {
        Status = "archived";
    }

    /// <summary>v5 §40 / 计划 G3：rollback 后写一条新的"回退到 X 版本"记录。</summary>
    public void MarkRolledBack(DateTime now, string targetSnapshotRef)
    {
        Status = "released";
        ReleasedAt = now;
        // 把 SnapshotRef 指向被回退到的目标快照，方便审计追溯
        SnapshotRef = targetSnapshotRef;
    }
}

/// <summary>
/// 兼容别名：旧 <c>KnowledgeVersionEntity</c>。新代码请使用 <see cref="KnowledgeDocumentVersion"/>。
/// 同样的 SugarTable，CodeFirst 不会再创建额外表；老调用方依然能 inject 旧仓储。
/// </summary>
[Obsolete("Renamed to KnowledgeDocumentVersion; will be removed in next major version.")]
[SugarTable("knowledge_document_version")]
public sealed class KnowledgeVersionEntity : TenantEntity
{
    public KnowledgeVersionEntity()
        : base(TenantId.Empty)
    {
        Label = string.Empty;
        SnapshotRef = string.Empty;
        CreatedBy = string.Empty;
        Status = "draft";
        CreatedAt = DateTime.UtcNow;
    }

    public KnowledgeVersionEntity(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        string label,
        string? note,
        string snapshotRef,
        int documentCount,
        int chunkCount,
        string createdBy,
        string status = "draft")
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        Label = label;
        Note = note;
        SnapshotRef = snapshotRef;
        DocumentCount = documentCount;
        ChunkCount = chunkCount;
        CreatedBy = createdBy;
        Status = string.IsNullOrWhiteSpace(status) ? "draft" : status;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "label", Length = 64)]
    public string Label { get; private set; }

    [SugarColumn(ColumnName = "note", IsNullable = true, Length = 2000)]
    public string? Note { get; private set; }

    [SugarColumn(ColumnName = "snapshot_ref", Length = 256)]
    public string SnapshotRef { get; private set; }

    [SugarColumn(ColumnName = "document_count")]
    public int DocumentCount { get; private set; }

    [SugarColumn(ColumnName = "chunk_count")]
    public int ChunkCount { get; private set; }

    [SugarColumn(ColumnName = "created_by", Length = 128)]
    public string CreatedBy { get; private set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; private set; }

    [SugarColumn(ColumnName = "released_at", IsNullable = true)]
    public DateTime? ReleasedAt { get; private set; }

    [SugarColumn(ColumnName = "status", Length = 32)]
    public string Status { get; private set; }

    public void Release(DateTime now)
    {
        Status = "released";
        ReleasedAt = now;
    }

    public void Archive()
    {
        Status = "archived";
    }
}

/* ====================================================================== */
/* v5 §35 / 计划 G2：KnowledgeJob 拆分 (Parse / Index / Rebuild / Gc)      */
/* ====================================================================== */

/// <summary>
/// 知识库任务实体（v5 §35/§37 / 计划 G2）。
/// 兼容解析、索引、重建、回收四类任务；保留为查询/聚合视图，新代码写入应使用
/// <see cref="KnowledgeParseJob"/> / <see cref="KnowledgeIndexJob"/> 等专用子表。
/// </summary>
[SugarTable("knowledge_job")]
public sealed class KnowledgeJob : TenantEntity
{
    public const string DefaultType = "parse";
    public const string StatusQueued = "Queued";

    public KnowledgeJob()
        : base(TenantId.Empty)
    {
        Type = DefaultType;
        Status = StatusQueued;
        LogsJson = "[]";
        EnqueuedAt = DateTime.UtcNow;
    }

    public KnowledgeJob(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        string type,
        long? documentId,
        string? payloadJson)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        Type = string.IsNullOrWhiteSpace(type) ? DefaultType : type;
        PayloadJson = payloadJson;
        Status = StatusQueued;
        Progress = 0;
        Attempts = 0;
        MaxAttempts = 3;
        EnqueuedAt = DateTime.UtcNow;
        LogsJson = "[]";
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id", IsNullable = true)]
    public long? DocumentId { get; private set; }

    [SugarColumn(ColumnName = "type", Length = 32)]
    public string Type { get; private set; }

    [SugarColumn(ColumnName = "status", Length = 32)]
    public string Status { get; private set; }

    [SugarColumn(ColumnName = "progress")]
    public int Progress { get; private set; }

    [SugarColumn(ColumnName = "attempts")]
    public int Attempts { get; private set; }

    [SugarColumn(ColumnName = "max_attempts")]
    public int MaxAttempts { get; private set; }

    [SugarColumn(ColumnName = "error_message", IsNullable = true, Length = 4000)]
    public string? ErrorMessage { get; private set; }

    [SugarColumn(ColumnName = "enqueued_at")]
    public DateTime EnqueuedAt { get; private set; }

    [SugarColumn(ColumnName = "started_at", IsNullable = true)]
    public DateTime? StartedAt { get; private set; }

    [SugarColumn(ColumnName = "finished_at", IsNullable = true)]
    public DateTime? FinishedAt { get; private set; }

    [SugarColumn(ColumnName = "payload_json", IsNullable = true, ColumnDataType = "text")]
    public string? PayloadJson { get; private set; }

    [SugarColumn(ColumnName = "logs_json", ColumnDataType = "text")]
    public string LogsJson { get; private set; }

    [SugarColumn(ColumnName = "hangfire_job_id", IsNullable = true, Length = 128)]
    public string? HangfireJobId { get; private set; }

    public void Start(DateTime now, string? hangfireJobId)
    {
        if (Status == "Canceled")
        {
            return;
        }
        Status = "Running";
        Progress = Math.Max(Progress, 1);
        StartedAt ??= now;
        Attempts = Math.Max(Attempts, 1);
        HangfireJobId = hangfireJobId;
    }

    public void Update(string status, int progress, string? errorMessage = null)
    {
        Status = status;
        Progress = Math.Clamp(progress, 0, 100);
        if (errorMessage is not null)
        {
            ErrorMessage = errorMessage;
        }
    }

    public void Finish(string status, int progress, string? errorMessage, DateTime now)
    {
        Status = status;
        Progress = Math.Clamp(progress, 0, 100);
        ErrorMessage = errorMessage;
        FinishedAt = now;
    }

    public void IncrementAttempts()
    {
        Attempts += 1;
    }

    public void AppendLogJson(string logsJson)
    {
        LogsJson = string.IsNullOrWhiteSpace(logsJson) ? "[]" : logsJson;
    }
}

/// <summary>
/// 解析任务专用表（v5 §35 / 计划 G2）。
/// 与 <see cref="KnowledgeJob"/> 相同字段集，但表名独立，便于 Hangfire 重试与运维查询。
/// 新代码应通过 <c>IKnowledgeParseJobService</c> 写入此表。
/// </summary>
[SugarTable("knowledge_parse_job")]
public sealed class KnowledgeParseJob : TenantEntity
{
    public KnowledgeParseJob()
        : base(TenantId.Empty)
    {
        Status = "Queued";
        LogsJson = "[]";
        EnqueuedAt = DateTime.UtcNow;
    }

    public KnowledgeParseJob(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        long documentId,
        string? parsingStrategyJson)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        ParsingStrategyJson = string.IsNullOrWhiteSpace(parsingStrategyJson) ? "{}" : parsingStrategyJson;
        Status = "Queued";
        Progress = 0;
        Attempts = 0;
        MaxAttempts = 3;
        EnqueuedAt = DateTime.UtcNow;
        LogsJson = "[]";
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id")]
    public long DocumentId { get; private set; }

    [SugarColumn(ColumnName = "parsing_strategy_json", ColumnDataType = "text")]
    public string ParsingStrategyJson { get; private set; } = "{}";

    [SugarColumn(ColumnName = "status", Length = 32)]
    public string Status { get; private set; }

    [SugarColumn(ColumnName = "progress")]
    public int Progress { get; private set; }

    [SugarColumn(ColumnName = "attempts")]
    public int Attempts { get; private set; }

    [SugarColumn(ColumnName = "max_attempts")]
    public int MaxAttempts { get; private set; }

    [SugarColumn(ColumnName = "error_message", IsNullable = true, Length = 4000)]
    public string? ErrorMessage { get; private set; }

    [SugarColumn(ColumnName = "enqueued_at")]
    public DateTime EnqueuedAt { get; private set; }

    [SugarColumn(ColumnName = "started_at", IsNullable = true)]
    public DateTime? StartedAt { get; private set; }

    [SugarColumn(ColumnName = "finished_at", IsNullable = true)]
    public DateTime? FinishedAt { get; private set; }

    [SugarColumn(ColumnName = "logs_json", ColumnDataType = "text")]
    public string LogsJson { get; private set; }

    [SugarColumn(ColumnName = "hangfire_job_id", IsNullable = true, Length = 128)]
    public string? HangfireJobId { get; private set; }

    public void Start(DateTime now, string? hangfireJobId)
    {
        if (Status == "Canceled") return;
        Status = "Running";
        Progress = Math.Max(Progress, 1);
        StartedAt ??= now;
        Attempts = Math.Max(Attempts, 1);
        HangfireJobId = hangfireJobId;
    }

    public void Update(string status, int progress, string? errorMessage = null)
    {
        Status = status;
        Progress = Math.Clamp(progress, 0, 100);
        if (errorMessage is not null) ErrorMessage = errorMessage;
    }

    public void Finish(string status, int progress, string? errorMessage, DateTime now)
    {
        Status = status;
        Progress = Math.Clamp(progress, 0, 100);
        ErrorMessage = errorMessage;
        FinishedAt = now;
    }

    public void IncrementAttempts() => Attempts += 1;
}

/// <summary>
/// 索引任务专用表（v5 §35 / 计划 G2）。
/// 与 <see cref="KnowledgeParseJob"/> 同结构，只是承载 ChunkingProfile + Mode（append / overwrite）。
/// </summary>
[SugarTable("knowledge_index_job")]
public sealed class KnowledgeIndexJob : TenantEntity
{
    public KnowledgeIndexJob()
        : base(TenantId.Empty)
    {
        Status = "Queued";
        LogsJson = "[]";
        EnqueuedAt = DateTime.UtcNow;
        Mode = "append";
    }

    public KnowledgeIndexJob(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        long documentId,
        string? chunkingProfileJson,
        string mode)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        ChunkingProfileJson = string.IsNullOrWhiteSpace(chunkingProfileJson) ? "{}" : chunkingProfileJson;
        Mode = string.IsNullOrWhiteSpace(mode) ? "append" : mode;
        Status = "Queued";
        Progress = 0;
        Attempts = 0;
        MaxAttempts = 3;
        EnqueuedAt = DateTime.UtcNow;
        LogsJson = "[]";
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id")]
    public long DocumentId { get; private set; }

    [SugarColumn(ColumnName = "chunking_profile_json", ColumnDataType = "text")]
    public string ChunkingProfileJson { get; private set; } = "{}";

    /// <summary>append / overwrite</summary>
    [SugarColumn(ColumnName = "mode", Length = 16)]
    public string Mode { get; private set; }

    [SugarColumn(ColumnName = "status", Length = 32)]
    public string Status { get; private set; }

    [SugarColumn(ColumnName = "progress")]
    public int Progress { get; private set; }

    [SugarColumn(ColumnName = "attempts")]
    public int Attempts { get; private set; }

    [SugarColumn(ColumnName = "max_attempts")]
    public int MaxAttempts { get; private set; }

    [SugarColumn(ColumnName = "error_message", IsNullable = true, Length = 4000)]
    public string? ErrorMessage { get; private set; }

    [SugarColumn(ColumnName = "enqueued_at")]
    public DateTime EnqueuedAt { get; private set; }

    [SugarColumn(ColumnName = "started_at", IsNullable = true)]
    public DateTime? StartedAt { get; private set; }

    [SugarColumn(ColumnName = "finished_at", IsNullable = true)]
    public DateTime? FinishedAt { get; private set; }

    [SugarColumn(ColumnName = "logs_json", ColumnDataType = "text")]
    public string LogsJson { get; private set; }

    [SugarColumn(ColumnName = "hangfire_job_id", IsNullable = true, Length = 128)]
    public string? HangfireJobId { get; private set; }

    public void Start(DateTime now, string? hangfireJobId)
    {
        if (Status == "Canceled") return;
        Status = "Running";
        Progress = Math.Max(Progress, 1);
        StartedAt ??= now;
        Attempts = Math.Max(Attempts, 1);
        HangfireJobId = hangfireJobId;
    }

    public void Update(string status, int progress, string? errorMessage = null)
    {
        Status = status;
        Progress = Math.Clamp(progress, 0, 100);
        if (errorMessage is not null) ErrorMessage = errorMessage;
    }

    public void Finish(string status, int progress, string? errorMessage, DateTime now)
    {
        Status = status;
        Progress = Math.Clamp(progress, 0, 100);
        ErrorMessage = errorMessage;
        FinishedAt = now;
    }

    public void IncrementAttempts() => Attempts += 1;
}

/* ====================================================================== */
/* v5 §39 / 计划 G2：KnowledgeBinding / Permission                        */
/* ====================================================================== */

/// <summary>
/// 知识库绑定关系（Agent / App / Workflow / Chatflow → KB）。
/// </summary>
[SugarTable("knowledge_binding")]
public sealed class KnowledgeBindingEntity : TenantEntity
{
    public KnowledgeBindingEntity()
        : base(TenantId.Empty)
    {
        CallerType = "agent";
        CallerId = string.Empty;
        CallerName = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public KnowledgeBindingEntity(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        string callerType,
        string callerId,
        string callerName,
        string? retrievalProfileOverrideJson)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        CallerType = callerType;
        CallerId = callerId;
        CallerName = callerName;
        RetrievalProfileOverrideJson = retrievalProfileOverrideJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "caller_type", Length = 32)]
    public string CallerType { get; private set; }

    [SugarColumn(ColumnName = "caller_id", Length = 128)]
    public string CallerId { get; private set; }

    [SugarColumn(ColumnName = "caller_name", Length = 256)]
    public string CallerName { get; private set; }

    [SugarColumn(ColumnName = "retrieval_profile_override_json", IsNullable = true, ColumnDataType = "text")]
    public string? RetrievalProfileOverrideJson { get; private set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; private set; }

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; private set; }

    public void Update(string callerName, string? retrievalProfileOverrideJson)
    {
        CallerName = callerName;
        RetrievalProfileOverrideJson = retrievalProfileOverrideJson;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 知识库权限（v5 §39 四层模型）。
/// </summary>
[SugarTable("knowledge_permission")]
public sealed class KnowledgePermissionEntity : TenantEntity
{
    public KnowledgePermissionEntity()
        : base(TenantId.Empty)
    {
        Scope = "kb";
        ScopeId = string.Empty;
        SubjectType = "user";
        SubjectId = string.Empty;
        SubjectName = string.Empty;
        ActionsJson = "[]";
        GrantedBy = string.Empty;
        GrantedAt = DateTime.UtcNow;
    }

    public KnowledgePermissionEntity(
        TenantId tenantId,
        long id,
        string scope,
        string scopeId,
        long? knowledgeBaseId,
        long? documentId,
        string subjectType,
        string subjectId,
        string subjectName,
        string actionsJson,
        string grantedBy)
        : base(tenantId)
    {
        Id = id;
        Scope = scope;
        ScopeId = scopeId;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        SubjectName = subjectName;
        ActionsJson = actionsJson;
        GrantedBy = grantedBy;
        GrantedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "scope", Length = 32)]
    public string Scope { get; private set; }

    [SugarColumn(ColumnName = "scope_id", Length = 128)]
    public string ScopeId { get; private set; }

    [SugarColumn(ColumnName = "knowledge_base_id", IsNullable = true)]
    public long? KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id", IsNullable = true)]
    public long? DocumentId { get; private set; }

    [SugarColumn(ColumnName = "subject_type", Length = 16)]
    public string SubjectType { get; private set; }

    [SugarColumn(ColumnName = "subject_id", Length = 128)]
    public string SubjectId { get; private set; }

    [SugarColumn(ColumnName = "subject_name", Length = 256)]
    public string SubjectName { get; private set; }

    [SugarColumn(ColumnName = "actions_json", ColumnDataType = "text")]
    public string ActionsJson { get; private set; }

    [SugarColumn(ColumnName = "granted_by", Length = 128)]
    public string GrantedBy { get; private set; }

    [SugarColumn(ColumnName = "granted_at")]
    public DateTime GrantedAt { get; private set; }

    public void UpdateActions(string actionsJson)
    {
        ActionsJson = actionsJson;
    }
}

/* ====================================================================== */
/* v5 §38 / 计划 G2：KnowledgeRetrievalLog                                 */
/* ====================================================================== */

[SugarTable("knowledge_retrieval_log")]
public sealed class KnowledgeRetrievalLogEntity : TenantEntity
{
    public KnowledgeRetrievalLogEntity()
        : base(TenantId.Empty)
    {
        TraceId = string.Empty;
        RawQuery = string.Empty;
        CallerContextJson = "{}";
        CandidatesJson = "[]";
        RerankedJson = "[]";
        FinalContext = string.Empty;
        EmbeddingModel = string.Empty;
        VectorStore = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public KnowledgeRetrievalLogEntity(
        TenantId tenantId,
        long id,
        string traceId,
        long knowledgeBaseId,
        string rawQuery,
        string? rewrittenQuery,
        string? filtersJson,
        string callerContextJson,
        string candidatesJson,
        string rerankedJson,
        string finalContext,
        string embeddingModel,
        string vectorStore,
        int latencyMs)
        : base(tenantId)
    {
        Id = id;
        TraceId = traceId;
        KnowledgeBaseId = knowledgeBaseId;
        RawQuery = rawQuery;
        RewrittenQuery = rewrittenQuery;
        FiltersJson = filtersJson;
        CallerContextJson = callerContextJson;
        CandidatesJson = candidatesJson;
        RerankedJson = rerankedJson;
        FinalContext = finalContext;
        EmbeddingModel = embeddingModel;
        VectorStore = vectorStore;
        LatencyMs = latencyMs;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "trace_id", Length = 64)]
    public string TraceId { get; private set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "raw_query", Length = 4000)]
    public string RawQuery { get; private set; }

    [SugarColumn(ColumnName = "rewritten_query", IsNullable = true, Length = 4000)]
    public string? RewrittenQuery { get; private set; }

    [SugarColumn(ColumnName = "filters_json", IsNullable = true, ColumnDataType = "text")]
    public string? FiltersJson { get; private set; }

    [SugarColumn(ColumnName = "caller_context_json", ColumnDataType = "text")]
    public string CallerContextJson { get; private set; }

    [SugarColumn(ColumnName = "candidates_json", ColumnDataType = "text")]
    public string CandidatesJson { get; private set; }

    [SugarColumn(ColumnName = "reranked_json", ColumnDataType = "text")]
    public string RerankedJson { get; private set; }

    [SugarColumn(ColumnName = "final_context", ColumnDataType = "text")]
    public string FinalContext { get; private set; }

    [SugarColumn(ColumnName = "embedding_model", Length = 128)]
    public string EmbeddingModel { get; private set; }

    [SugarColumn(ColumnName = "vector_store", Length = 64)]
    public string VectorStore { get; private set; }

    [SugarColumn(ColumnName = "latency_ms")]
    public int LatencyMs { get; private set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; private set; }
}

/* ====================================================================== */
/* v5 §39 / 计划 G2：Provider 配置                                          */
/* ====================================================================== */

[SugarTable("knowledge_provider_config")]
public sealed class KnowledgeProviderConfigEntity : TenantEntity
{
    public KnowledgeProviderConfigEntity()
        : base(TenantId.Empty)
    {
        ConfigId = string.Empty;
        Role = "vector";
        ProviderName = string.Empty;
        DisplayName = string.Empty;
        Status = "active";
        UpdatedAt = DateTime.UtcNow;
    }

    public KnowledgeProviderConfigEntity(
        TenantId tenantId,
        long id,
        string configId,
        string role,
        string providerName,
        string displayName,
        bool isDefault,
        string status,
        string? endpoint,
        string? region,
        string? bucketOrIndex,
        string? metadataJson)
        : base(tenantId)
    {
        Id = id;
        ConfigId = configId;
        Role = role;
        ProviderName = providerName;
        DisplayName = displayName;
        IsDefault = isDefault;
        Status = string.IsNullOrWhiteSpace(status) ? "active" : status;
        Endpoint = endpoint;
        Region = region;
        BucketOrIndex = bucketOrIndex;
        MetadataJson = metadataJson;
        UpdatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "config_id", Length = 128)]
    public string ConfigId { get; private set; }

    /// <summary>upload / storage / vector / embedding / generation</summary>
    [SugarColumn(ColumnName = "role", Length = 16)]
    public string Role { get; private set; }

    [SugarColumn(ColumnName = "provider_name", Length = 128)]
    public string ProviderName { get; private set; }

    [SugarColumn(ColumnName = "display_name", Length = 256)]
    public string DisplayName { get; private set; }

    [SugarColumn(ColumnName = "is_default")]
    public bool IsDefault { get; private set; }

    [SugarColumn(ColumnName = "status", Length = 16)]
    public string Status { get; private set; }

    [SugarColumn(ColumnName = "endpoint", IsNullable = true, Length = 512)]
    public string? Endpoint { get; private set; }

    [SugarColumn(ColumnName = "region", IsNullable = true, Length = 128)]
    public string? Region { get; private set; }

    [SugarColumn(ColumnName = "bucket_or_index", IsNullable = true, Length = 256)]
    public string? BucketOrIndex { get; private set; }

    [SugarColumn(ColumnName = "metadata_json", IsNullable = true, ColumnDataType = "text")]
    public string? MetadataJson { get; private set; }

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; private set; }

    public void UpdateStatus(string status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetIsDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Upsert(
        string providerName,
        string displayName,
        string status,
        bool isDefault,
        string? endpoint,
        string? region,
        string? bucketOrIndex,
        string? metadataJson)
    {
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? ProviderName : providerName;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? DisplayName : displayName;
        Status = string.IsNullOrWhiteSpace(status) ? Status : status;
        IsDefault = isDefault;
        Endpoint = endpoint;
        Region = region;
        BucketOrIndex = bucketOrIndex;
        MetadataJson = metadataJson;
        UpdatedAt = DateTime.UtcNow;
    }
}

/* ====================================================================== */
/* v5 §37 / 计划 G2：表格 KB 三表（KnowledgeTable + Column + Row）           */
/* ====================================================================== */

/// <summary>
/// 表格知识库父表（v5 §37 / 计划 G2 新增）。
/// 一份表格文档对应 1..N 个 KnowledgeTable（每个 sheet 一个），其下挂 Column + Row。
/// </summary>
[SugarTable("knowledge_table")]
public sealed class KnowledgeTable : TenantEntity
{
    public KnowledgeTable()
        : base(TenantId.Empty)
    {
        SheetId = string.Empty;
        DisplayName = string.Empty;
    }

    public KnowledgeTable(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        long documentId,
        string sheetId,
        string displayName,
        int rowCount,
        int columnCount)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        SheetId = string.IsNullOrWhiteSpace(sheetId) ? "default" : sheetId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? sheetId : displayName;
        RowCount = rowCount;
        ColumnCount = columnCount;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id")]
    public long DocumentId { get; private set; }

    /// <summary>多 sheet 文档的 sheet 标识；单 sheet 时取 "default"。</summary>
    [SugarColumn(ColumnName = "sheet_id", Length = 128)]
    public string SheetId { get; private set; }

    [SugarColumn(ColumnName = "display_name", Length = 256)]
    public string DisplayName { get; private set; }

    [SugarColumn(ColumnName = "row_count")]
    public int RowCount { get; private set; }

    [SugarColumn(ColumnName = "column_count")]
    public int ColumnCount { get; private set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; private set; }

    public void UpdateCounts(int rowCount, int columnCount)
    {
        RowCount = rowCount;
        ColumnCount = columnCount;
    }
}

[SugarTable("knowledge_table_column")]
public sealed class KnowledgeTableColumnEntity : TenantEntity
{
    public KnowledgeTableColumnEntity()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        DataType = "string";
    }

    public KnowledgeTableColumnEntity(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        long documentId,
        int ordinal,
        string name,
        bool isIndexColumn,
        string dataType,
        long? tableId = null)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        TableId = tableId;
        Ordinal = ordinal;
        Name = name;
        IsIndexColumn = isIndexColumn;
        DataType = string.IsNullOrWhiteSpace(dataType) ? "string" : dataType;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id")]
    public long DocumentId { get; private set; }

    /// <summary>父表 KnowledgeTable.Id；旧数据可能为 null。</summary>
    [SugarColumn(ColumnName = "table_id", IsNullable = true)]
    public long? TableId { get; private set; }

    [SugarColumn(ColumnName = "ordinal")]
    public int Ordinal { get; private set; }

    [SugarColumn(ColumnName = "name", Length = 256)]
    public string Name { get; private set; }

    [SugarColumn(ColumnName = "is_index_column")]
    public bool IsIndexColumn { get; private set; }

    /// <summary>string / number / boolean / date</summary>
    [SugarColumn(ColumnName = "data_type", Length = 16)]
    public string DataType { get; private set; }
}

[SugarTable("knowledge_table_row")]
public sealed class KnowledgeTableRowEntity : TenantEntity
{
    public KnowledgeTableRowEntity()
        : base(TenantId.Empty)
    {
        CellsJson = "{}";
    }

    public KnowledgeTableRowEntity(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        long documentId,
        int rowIndex,
        string cellsJson,
        long? chunkId,
        long? tableId = null)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        TableId = tableId;
        RowIndex = rowIndex;
        CellsJson = string.IsNullOrWhiteSpace(cellsJson) ? "{}" : cellsJson;
        ChunkId = chunkId;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id")]
    public long DocumentId { get; private set; }

    [SugarColumn(ColumnName = "table_id", IsNullable = true)]
    public long? TableId { get; private set; }

    [SugarColumn(ColumnName = "row_index")]
    public int RowIndex { get; private set; }

    [SugarColumn(ColumnName = "cells_json", ColumnDataType = "text")]
    public string CellsJson { get; private set; }

    [SugarColumn(ColumnName = "chunk_id", IsNullable = true)]
    public long? ChunkId { get; private set; }
}

/* ====================================================================== */
/* v5 §37 / 计划 G2：图片 KB 两表                                           */
/* ====================================================================== */

[SugarTable("knowledge_image_item")]
public sealed class KnowledgeImageItemEntity : TenantEntity
{
    public KnowledgeImageItemEntity()
        : base(TenantId.Empty)
    {
        FileName = string.Empty;
    }

    public KnowledgeImageItemEntity(
        TenantId tenantId,
        long id,
        long knowledgeBaseId,
        long documentId,
        string fileName,
        long? fileId,
        int? width,
        int? height,
        string? thumbnailUrl)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        FileName = fileName;
        FileId = fileId;
        Width = width;
        Height = height;
        ThumbnailUrl = thumbnailUrl;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "document_id")]
    public long DocumentId { get; private set; }

    [SugarColumn(ColumnName = "file_name", Length = 256)]
    public string FileName { get; private set; }

    [SugarColumn(ColumnName = "file_id", IsNullable = true)]
    public long? FileId { get; private set; }

    [SugarColumn(ColumnName = "width", IsNullable = true)]
    public int? Width { get; private set; }

    [SugarColumn(ColumnName = "height", IsNullable = true)]
    public int? Height { get; private set; }

    [SugarColumn(ColumnName = "thumbnail_url", IsNullable = true, Length = 512)]
    public string? ThumbnailUrl { get; private set; }
}

[SugarTable("knowledge_image_annotation")]
public sealed class KnowledgeImageAnnotationEntity : TenantEntity
{
    public KnowledgeImageAnnotationEntity()
        : base(TenantId.Empty)
    {
        Type = "caption";
        Text = string.Empty;
    }

    public KnowledgeImageAnnotationEntity(
        TenantId tenantId,
        long id,
        long imageItemId,
        string type,
        string text,
        float? confidence)
        : base(tenantId)
    {
        Id = id;
        ImageItemId = imageItemId;
        Type = string.IsNullOrWhiteSpace(type) ? "caption" : type;
        Text = text;
        Confidence = confidence;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "image_item_id")]
    public long ImageItemId { get; private set; }

    /// <summary>caption / ocr / tag / vlm</summary>
    [SugarColumn(ColumnName = "type", Length = 16)]
    public string Type { get; private set; }

    [SugarColumn(ColumnName = "text", Length = 4000)]
    public string Text { get; private set; }

    [SugarColumn(ColumnName = "confidence", IsNullable = true)]
    public float? Confidence { get; private set; }
}

/* ====================================================================== */
/* v5 §32-44 / 计划 G2：sidecar 元数据（kb / document）                     */
/* ====================================================================== */

[SugarTable("knowledge_base_meta")]
public sealed class KnowledgeBaseMetaEntity : TenantEntity
{
    public KnowledgeBaseMetaEntity()
        : base(TenantId.Empty)
    {
        Kind = "text";
        ProviderKind = "builtin";
        TagsJson = "[]";
        ChunkingProfileJson = "{}";
        RetrievalProfileJson = "{}";
        LifecycleStatus = "Ready";
        VersionLabel = "v0";
        UpdatedAt = DateTime.UtcNow;
    }

    public KnowledgeBaseMetaEntity(
        TenantId tenantId,
        long knowledgeBaseId,
        string kind,
        string providerKind,
        string? providerConfigId,
        string? tagsJson,
        string? chunkingProfileJson,
        string? retrievalProfileJson,
        string lifecycleStatus,
        string versionLabel,
        string? ownerName)
        : base(tenantId)
    {
        Id = knowledgeBaseId;
        KnowledgeBaseId = knowledgeBaseId;
        Kind = string.IsNullOrWhiteSpace(kind) ? "text" : kind;
        ProviderKind = string.IsNullOrWhiteSpace(providerKind) ? "builtin" : providerKind;
        ProviderConfigId = providerConfigId;
        TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        ChunkingProfileJson = string.IsNullOrWhiteSpace(chunkingProfileJson) ? "{}" : chunkingProfileJson;
        RetrievalProfileJson = string.IsNullOrWhiteSpace(retrievalProfileJson) ? "{}" : retrievalProfileJson;
        LifecycleStatus = string.IsNullOrWhiteSpace(lifecycleStatus) ? "Ready" : lifecycleStatus;
        VersionLabel = string.IsNullOrWhiteSpace(versionLabel) ? "v0" : versionLabel;
        OwnerName = ownerName;
        UpdatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    /// <summary>text / table / image</summary>
    [SugarColumn(ColumnName = "kind", Length = 16)]
    public string Kind { get; private set; }

    /// <summary>builtin / qdrant / external</summary>
    [SugarColumn(ColumnName = "provider_kind", Length = 16)]
    public string ProviderKind { get; private set; }

    [SugarColumn(ColumnName = "provider_config_id", IsNullable = true, Length = 128)]
    public string? ProviderConfigId { get; private set; }

    [SugarColumn(ColumnName = "tags_json", ColumnDataType = "text")]
    public string TagsJson { get; private set; }

    [SugarColumn(ColumnName = "chunking_profile_json", ColumnDataType = "text")]
    public string ChunkingProfileJson { get; private set; }

    [SugarColumn(ColumnName = "retrieval_profile_json", ColumnDataType = "text")]
    public string RetrievalProfileJson { get; private set; }

    [SugarColumn(ColumnName = "lifecycle_status", Length = 32)]
    public string LifecycleStatus { get; private set; }

    [SugarColumn(ColumnName = "version_label", Length = 64)]
    public string VersionLabel { get; private set; }

    [SugarColumn(ColumnName = "owner_name", IsNullable = true, Length = 256)]
    public string? OwnerName { get; private set; }

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string? kind,
        string? providerKind,
        string? providerConfigId,
        string? tagsJson,
        string? chunkingProfileJson,
        string? retrievalProfileJson,
        string? lifecycleStatus,
        string? versionLabel,
        string? ownerName)
    {
        if (!string.IsNullOrWhiteSpace(kind)) Kind = kind;
        if (!string.IsNullOrWhiteSpace(providerKind)) ProviderKind = providerKind;
        if (!string.IsNullOrWhiteSpace(providerConfigId)) ProviderConfigId = providerConfigId;
        if (tagsJson is not null) TagsJson = string.IsNullOrWhiteSpace(tagsJson) ? "[]" : tagsJson;
        if (chunkingProfileJson is not null) ChunkingProfileJson = string.IsNullOrWhiteSpace(chunkingProfileJson) ? "{}" : chunkingProfileJson;
        if (retrievalProfileJson is not null) RetrievalProfileJson = string.IsNullOrWhiteSpace(retrievalProfileJson) ? "{}" : retrievalProfileJson;
        if (!string.IsNullOrWhiteSpace(lifecycleStatus)) LifecycleStatus = lifecycleStatus;
        if (!string.IsNullOrWhiteSpace(versionLabel)) VersionLabel = versionLabel;
        if (ownerName is not null) OwnerName = ownerName;
        UpdatedAt = DateTime.UtcNow;
    }
}

[SugarTable("knowledge_document_meta")]
public sealed class KnowledgeDocumentMetaEntity : TenantEntity
{
    public KnowledgeDocumentMetaEntity()
        : base(TenantId.Empty)
    {
        LifecycleStatus = "Ready";
        ParsingStrategyJson = "{}";
        VersionLabel = "v0";
        UpdatedAt = DateTime.UtcNow;
    }

    public KnowledgeDocumentMetaEntity(
        TenantId tenantId,
        long documentId,
        long knowledgeBaseId,
        string lifecycleStatus,
        string parsingStrategyJson,
        long? parseJobId,
        long? indexJobId,
        string versionLabel,
        string? ownerUserId)
        : base(tenantId)
    {
        Id = documentId;
        DocumentId = documentId;
        KnowledgeBaseId = knowledgeBaseId;
        LifecycleStatus = string.IsNullOrWhiteSpace(lifecycleStatus) ? "Ready" : lifecycleStatus;
        ParsingStrategyJson = string.IsNullOrWhiteSpace(parsingStrategyJson) ? "{}" : parsingStrategyJson;
        ParseJobId = parseJobId;
        IndexJobId = indexJobId;
        VersionLabel = string.IsNullOrWhiteSpace(versionLabel) ? "v0" : versionLabel;
        OwnerUserId = ownerUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    [SugarColumn(IsPrimaryKey = true, ColumnName = "id")]
    public new long Id { get; set; }

    [SugarColumn(ColumnName = "document_id")]
    public long DocumentId { get; private set; }

    [SugarColumn(ColumnName = "knowledge_base_id")]
    public long KnowledgeBaseId { get; private set; }

    [SugarColumn(ColumnName = "lifecycle_status", Length = 32)]
    public string LifecycleStatus { get; private set; }

    [SugarColumn(ColumnName = "parsing_strategy_json", ColumnDataType = "text")]
    public string ParsingStrategyJson { get; private set; }

    [SugarColumn(ColumnName = "parse_job_id", IsNullable = true)]
    public long? ParseJobId { get; private set; }

    [SugarColumn(ColumnName = "index_job_id", IsNullable = true)]
    public long? IndexJobId { get; private set; }

    [SugarColumn(ColumnName = "version_label", Length = 64)]
    public string VersionLabel { get; private set; }

    [SugarColumn(ColumnName = "owner_user_id", IsNullable = true, Length = 128)]
    public string? OwnerUserId { get; private set; }

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; private set; }

    public void SetLifecycle(string lifecycleStatus)
    {
        if (!string.IsNullOrWhiteSpace(lifecycleStatus))
        {
            LifecycleStatus = lifecycleStatus;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetJobs(long? parseJobId, long? indexJobId)
    {
        ParseJobId = parseJobId;
        IndexJobId = indexJobId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetParsingStrategy(string parsingStrategyJson)
    {
        ParsingStrategyJson = string.IsNullOrWhiteSpace(parsingStrategyJson) ? "{}" : parsingStrategyJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
