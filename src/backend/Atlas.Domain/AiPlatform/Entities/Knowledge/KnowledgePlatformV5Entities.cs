using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities.Knowledge;

/// <summary>
/// 知识库版本快照（v5 §40）。snapshot_ref 指向已固化的 Schema 元信息文件 / 行；
/// 回退仅恢复 Schema，已上传文件保留。
/// </summary>
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

    public long KnowledgeBaseId { get; private set; }
    public string Label { get; private set; }
    public string? Note { get; private set; }
    public string SnapshotRef { get; private set; }
    public int DocumentCount { get; private set; }
    public int ChunkCount { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
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

/// <summary>
/// 知识库异步任务实体（v5 §35/§37）。
/// 兼容解析、索引、重建、回收四类任务，PayloadJson 存放各自任务参数（如 ParsingStrategy）。
/// Status / Type 持久化为字符串，避免迁移现有 KnowledgeJobService 的 string 常量映射。
/// </summary>
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

    public long KnowledgeBaseId { get; private set; }
    public long? DocumentId { get; private set; }
    /// <summary>parse / index / rebuild / gc</summary>
    public string Type { get; private set; }
    /// <summary>Queued / Running / Succeeded / Failed / Retrying / DeadLetter / Canceled</summary>
    public string Status { get; private set; }
    public int Progress { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime EnqueuedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string? PayloadJson { get; private set; }
    /// <summary>JSON array of <c>{ ts, level, message }</c> 日志条目。</summary>
    public string LogsJson { get; private set; }
    /// <summary>Hangfire backgroundJob.Id（如果通过 Hangfire 调度）。</summary>
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
/// 知识库绑定关系（Agent / App / Workflow / Chatflow → KB）。
/// 删除 KB 之前必须列出所有绑定，依赖检查见 v5 §39。
/// </summary>
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

    public long KnowledgeBaseId { get; private set; }
    /// <summary>agent / app / workflow / chatflow</summary>
    public string CallerType { get; private set; }
    public string CallerId { get; private set; }
    public string CallerName { get; private set; }
    public string? RetrievalProfileOverrideJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
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
/// scope: space / project / kb / document；ActionsJson: ["view","retrieve",...]
/// </summary>
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

    public string Scope { get; private set; }
    public string ScopeId { get; private set; }
    public long? KnowledgeBaseId { get; private set; }
    public long? DocumentId { get; private set; }
    public string SubjectType { get; private set; }
    public string SubjectId { get; private set; }
    public string SubjectName { get; private set; }
    /// <summary>JSON array of action strings (view/edit/delete/publish/manage/retrieve).</summary>
    public string ActionsJson { get; private set; }
    public string GrantedBy { get; private set; }
    public DateTime GrantedAt { get; private set; }

    public void UpdateActions(string actionsJson)
    {
        ActionsJson = actionsJson;
    }
}

/// <summary>
/// 检索调用日志（v5 §38 召回透明度）。
/// 一次检索调用 = 一条 RetrievalLog；前端调试面板按 traceId 查询。
/// </summary>
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

    public string TraceId { get; private set; }
    public long KnowledgeBaseId { get; private set; }
    public string RawQuery { get; private set; }
    public string? RewrittenQuery { get; private set; }
    public string? FiltersJson { get; private set; }
    public string CallerContextJson { get; private set; }
    /// <summary>JSON array of RetrievalCandidate.</summary>
    public string CandidatesJson { get; private set; }
    /// <summary>JSON array of RetrievalCandidate with rerank scores.</summary>
    public string RerankedJson { get; private set; }
    public string FinalContext { get; private set; }
    public string EmbeddingModel { get; private set; }
    public string VectorStore { get; private set; }
    public int LatencyMs { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

/// <summary>
/// Provider 配置（v5 §39 Provider 抽象）。
/// id 是字符串（如 "vector-qdrant-default"），便于跨租户共享同一份默认配置。
/// </summary>
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

    public string ConfigId { get; private set; }
    /// <summary>upload / storage / vector / embedding / generation</summary>
    public string Role { get; private set; }
    public string ProviderName { get; private set; }
    public string DisplayName { get; private set; }
    public bool IsDefault { get; private set; }
    /// <summary>active / degraded / inactive</summary>
    public string Status { get; private set; }
    public string? Endpoint { get; private set; }
    public string? Region { get; private set; }
    public string? BucketOrIndex { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void UpdateStatus(string status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 表格知识库的列定义（v5 §37）。
/// 与 DocumentChunk 协同：表格行切片在 ColumnHeadersJson 内冗余列名，但权威定义在此表。
/// </summary>
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
        string dataType)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        Ordinal = ordinal;
        Name = name;
        IsIndexColumn = isIndexColumn;
        DataType = string.IsNullOrWhiteSpace(dataType) ? "string" : dataType;
    }

    public long KnowledgeBaseId { get; private set; }
    public long DocumentId { get; private set; }
    public int Ordinal { get; private set; }
    public string Name { get; private set; }
    public bool IsIndexColumn { get; private set; }
    /// <summary>string / number / boolean / date</summary>
    public string DataType { get; private set; }
}

/// <summary>表格知识库的行（v5 §37）。CellsJson 是 <c>{"姓名":"李明",...}</c>。</summary>
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
        long? chunkId)
        : base(tenantId)
    {
        Id = id;
        KnowledgeBaseId = knowledgeBaseId;
        DocumentId = documentId;
        RowIndex = rowIndex;
        CellsJson = string.IsNullOrWhiteSpace(cellsJson) ? "{}" : cellsJson;
        ChunkId = chunkId;
    }

    public long KnowledgeBaseId { get; private set; }
    public long DocumentId { get; private set; }
    public int RowIndex { get; private set; }
    public string CellsJson { get; private set; }
    public long? ChunkId { get; private set; }
}

/// <summary>图片知识库的图片项（v5 §37）。</summary>
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

    public long KnowledgeBaseId { get; private set; }
    public long DocumentId { get; private set; }
    public string FileName { get; private set; }
    public long? FileId { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string? ThumbnailUrl { get; private set; }
}

/// <summary>图片标注（caption / ocr / tag / vlm），v5 §37。</summary>
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

    public long ImageItemId { get; private set; }
    /// <summary>caption / ocr / tag / vlm</summary>
    public string Type { get; private set; }
    public string Text { get; private set; }
    public float? Confidence { get; private set; }
}

/// <summary>
/// 知识库元数据（v5 §32-44）：把 v5 报告新增的可选字段（kind / providerKind / chunkingProfile /
/// retrievalProfile / lifecycleStatus / tags / versionLabel / ownerName）以 JSON 集中存放，
/// 避免改动旧 <see cref="KnowledgeBase"/> 字段顺序与签名（保持向后兼容）。
/// </summary>
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
        // Id 强制等于 KnowledgeBaseId，1:1 关系
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

    public long KnowledgeBaseId { get; private set; }
    /// <summary>text / table / image</summary>
    public string Kind { get; private set; }
    /// <summary>builtin / qdrant / external</summary>
    public string ProviderKind { get; private set; }
    public string? ProviderConfigId { get; private set; }
    public string TagsJson { get; private set; }
    public string ChunkingProfileJson { get; private set; }
    public string RetrievalProfileJson { get; private set; }
    public string LifecycleStatus { get; private set; }
    public string VersionLabel { get; private set; }
    public string? OwnerName { get; private set; }
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

/// <summary>
/// 文档级 v5 元数据（lifecycleStatus / parsingStrategy / parseJobId / indexJobId / versionLabel）。
/// 与 <see cref="KnowledgeBaseMetaEntity"/> 相同思路：1:1 sidecar，避免破坏旧 KnowledgeDocument 签名。
/// </summary>
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

    public long DocumentId { get; private set; }
    public long KnowledgeBaseId { get; private set; }
    public string LifecycleStatus { get; private set; }
    public string ParsingStrategyJson { get; private set; }
    public long? ParseJobId { get; private set; }
    public long? IndexJobId { get; private set; }
    public string VersionLabel { get; private set; }
    public string? OwnerUserId { get; private set; }
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
