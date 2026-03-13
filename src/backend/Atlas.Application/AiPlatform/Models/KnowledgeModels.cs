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
    KnowledgeBaseType Type);

public sealed record KnowledgeBaseUpdateRequest(
    string Name,
    string? Description,
    KnowledgeBaseType Type);

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
    DateTime? ProcessedAt);

public sealed record DocumentCreateRequest(long FileId);

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
    DateTime CreatedAt);

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
    int Overlap = 50);

public sealed record RagSearchResult(
    long KnowledgeBaseId,
    long DocumentId,
    long ChunkId,
    string Content,
    float Score,
    string? DocumentName);
