using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record KnowledgeBaseDto(
    long Id,
    string Name,
    string? Description,
    KnowledgeBaseType Type,
    int DocumentCount,
    int ChunkCount,
    DateTime CreatedAt);

public sealed record KnowledgeBaseCreateRequest(
    string Name,
    string? Description,
    KnowledgeBaseType Type,
    long? WorkspaceId = null);

public sealed record KnowledgeBaseUpdateRequest(
    string Name,
    string? Description,
    KnowledgeBaseType Type,
    long? WorkspaceId = null);

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
    string ImageMetadataJson);

public sealed record DocumentCreateRequest(
    long FileId,
    string? TagsJson = null,
    string? ImageMetadataJson = null);

public sealed record DocumentProgressDto(
    long Id,
    DocumentProcessingStatus Status,
    int ChunkCount,
    string? ErrorMessage,
    DateTime? ProcessedAt);

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
    DocumentParseStrategy ParseStrategy = DocumentParseStrategy.Quick);

public sealed record KnowledgeRetrievalTestRequest(
    string Query,
    int TopK = 5,
    IReadOnlyList<long>? KnowledgeBaseIds = null,
    IReadOnlyList<string>? Tags = null,
    float? MinScore = null,
    int Offset = 0,
    string? OwnerFilter = null,
    IReadOnlyDictionary<string, string>? MetadataFilter = null);

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
    string? DocumentNamespace = null);
