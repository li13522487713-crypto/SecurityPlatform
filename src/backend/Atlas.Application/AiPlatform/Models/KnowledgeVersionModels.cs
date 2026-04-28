namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgeVersionStatus
{
    Draft = 0,
    Released = 1,
    Archived = 2
}

public sealed record KnowledgeVersionDto(
    long Id,
    long KnowledgeBaseId,
    string Label,
    KnowledgeVersionStatus Status,
    string SnapshotRef,
    int DocumentCount,
    int ChunkCount,
    string CreatedBy,
    DateTime CreatedAt,
    string? Note = null,
    DateTime? ReleasedAt = null);

public sealed record KnowledgeVersionCreateRequest(
    string Label,
    string? Note = null);

public sealed record KnowledgeVersionDiffEntry(
    string Kind,
    string ChangeType,
    string Ref,
    string Summary);

public sealed record KnowledgeVersionDiffDto(
    long FromVersionId,
    long ToVersionId,
    IReadOnlyList<KnowledgeVersionDiffEntry> Entries);
