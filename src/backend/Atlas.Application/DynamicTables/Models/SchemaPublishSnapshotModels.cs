namespace Atlas.Application.DynamicTables.Models;

public sealed record SchemaPublishSnapshotListItem(
    long Id,
    string TableKey,
    int Version,
    string? PublishNote,
    long PublishedBy,
    DateTimeOffset PublishedAt,
    long? MigrationTaskId);

public sealed record SchemaPublishSnapshotDetail(
    long Id,
    long TableId,
    string TableKey,
    int Version,
    string SnapshotJson,
    string? PublishNote,
    long PublishedBy,
    DateTimeOffset PublishedAt,
    long? MigrationTaskId);

public sealed record SchemaPublishSnapshotCreateRequest(
    string TableKey,
    string? PublishNote);

public sealed record SchemaSnapshotDiffResult(
    int FromVersion,
    int ToVersion,
    string TableKey,
    IReadOnlyList<SchemaFieldDiff> FieldChanges,
    IReadOnlyList<SchemaIndexDiff> IndexChanges);

public sealed record SchemaFieldDiff(
    string FieldName,
    string ChangeType,
    string? OldDefinition,
    string? NewDefinition);

public sealed record SchemaIndexDiff(
    string IndexName,
    string ChangeType,
    string? OldDefinition,
    string? NewDefinition);
