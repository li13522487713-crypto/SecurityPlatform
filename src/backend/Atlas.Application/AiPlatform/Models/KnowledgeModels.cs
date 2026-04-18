using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record KnowledgeBaseDto(
    long Id,
    string Name,
    string? Description,
    KnowledgeBaseType Type,
    int DocumentCount,
    int ChunkCount,
    DateTime CreatedAt,
    // ↓ v5 §32-44 扩展字段（向后兼容：旧调用方按位置构造时仍合法）
    KnowledgeBaseKind? Kind = null,
    KnowledgeBaseProviderKind? ProviderKind = null,
    string? ProviderConfigId = null,
    KnowledgeDocumentLifecycleStatus? LifecycleStatus = null,
    ChunkingProfile? ChunkingProfile = null,
    RetrievalProfile? RetrievalProfile = null,
    IReadOnlyList<string>? Tags = null,
    int? BindingCount = null,
    int? PendingJobCount = null,
    int? FailedJobCount = null,
    string? VersionLabel = null,
    DateTime? UpdatedAt = null,
    string? OwnerName = null,
    long? WorkspaceId = null);

public sealed record KnowledgeBaseCreateRequest(
    string Name,
    string? Description,
    KnowledgeBaseType Type,
    long? WorkspaceId = null,
    // v5 扩展
    KnowledgeBaseKind? Kind = null,
    KnowledgeBaseProviderKind? ProviderKind = null,
    string? ProviderConfigId = null,
    ChunkingProfile? ChunkingProfile = null,
    RetrievalProfile? RetrievalProfile = null,
    IReadOnlyList<string>? Tags = null);

public sealed record KnowledgeBaseUpdateRequest(
    string Name,
    string? Description,
    KnowledgeBaseType Type,
    long? WorkspaceId = null,
    // v5 扩展
    KnowledgeBaseKind? Kind = null,
    KnowledgeBaseProviderKind? ProviderKind = null,
    string? ProviderConfigId = null,
    ChunkingProfile? ChunkingProfile = null,
    RetrievalProfile? RetrievalProfile = null,
    IReadOnlyList<string>? Tags = null);

public sealed record KnowledgeDocumentDto(
    long Id,
    long KnowledgeBaseId,
    long? FileId,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    DocumentProcessingStatus Status,
    string? ErrorMessage,
    int ChunkCount,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string TagsJson,
    string ImageMetadataJson,
    // v5 扩展
    KnowledgeDocumentLifecycleStatus? LifecycleStatus = null,
    ParsingStrategy? ParsingStrategy = null,
    long? ParseJobId = null,
    long? IndexJobId = null,
    string? VersionLabel = null,
    string? OwnerUserId = null);

public sealed record DocumentCreateRequest(
    long FileId,
    string? TagsJson = null,
    string? ImageMetadataJson = null,
    // v5 扩展：上传时携带的解析策略
    ParsingStrategy? ParsingStrategy = null);

public sealed record DocumentProgressDto(
    long Id,
    DocumentProcessingStatus Status,
    int ChunkCount,
    string? ErrorMessage,
    DateTime? ProcessedAt,
    // v5 扩展
    KnowledgeDocumentLifecycleStatus? LifecycleStatus = null,
    long? ParseJobId = null,
    long? IndexJobId = null);

public sealed record DocumentChunkDto(
    long Id,
    long KnowledgeBaseId,
    long DocumentId,
    int ChunkIndex,
    string Content,
    int StartOffset,
    int EndOffset,
    bool HasEmbedding,
    DateTime CreatedAt,
    int? RowIndex,
    string? ColumnHeadersJson);

public sealed record ChunkCreateRequest(
    long DocumentId,
    int ChunkIndex,
    string Content,
    int StartOffset,
    int EndOffset);

public sealed record ChunkUpdateRequest(
    string Content,
    int StartOffset,
    int EndOffset);

public sealed record DocumentResegmentRequest(
    int ChunkSize = 500,
    int Overlap = 50,
    ChunkingStrategy Strategy = ChunkingStrategy.Fixed,
    DocumentParseStrategy ParseStrategy = DocumentParseStrategy.Quick,
    // v5 扩展：携带完整 ParsingStrategy / ChunkingProfile，会覆盖标量字段
    ParsingStrategy? ParsingStrategy = null,
    ChunkingProfile? ChunkingProfile = null);

public sealed record KnowledgeRetrievalTestRequest(
    string Query,
    int TopK = 5,
    IReadOnlyList<long>? KnowledgeBaseIds = null,
    IReadOnlyList<string>? Tags = null,
    float? MinScore = null,
    int Offset = 0,
    string? OwnerFilter = null,
    IReadOnlyDictionary<string, string>? MetadataFilter = null,
    // v5 扩展：透传完整 caller_context、retrievalProfile、debug
    RetrievalProfile? RetrievalProfile = null,
    RetrievalCallerContext? CallerContext = null,
    bool Debug = false);

/// <summary>检索后过滤与分页（向量/BM25 命中后再应用）。</summary>
public sealed record RagRetrievalFilter(
    IReadOnlyList<string>? Tags = null,
    float? MinScore = null,
    int Offset = 0,
    string? OwnerFilter = null,
    IReadOnlyDictionary<string, string>? MetadataFilter = null);

public sealed record RagSearchResult(
    long KnowledgeBaseId,
    long DocumentId,
    long ChunkId,
    string Content,
    float Score,
    string? DocumentName,
    DateTime? DocumentCreatedAt = null,
    int StartOffset = 0,
    int EndOffset = 0,
    string? TagsJson = null,
    string? DocumentNamespace = null,
    // v5 扩展：rerank score 与命中源
    float? RerankScore = null,
    RetrievalCandidateSource Source = RetrievalCandidateSource.Vector);
