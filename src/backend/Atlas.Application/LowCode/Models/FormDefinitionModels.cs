namespace Atlas.Application.LowCode.Models;

public sealed record FormDefinitionListItem(
    string Id,
    string Name,
    string? Description,
    string? Category,
    int Version,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long CreatedBy,
    string? DataTableKey,
    string? Icon,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? DeprecatedAt)
{
    public bool IsDeprecated => DeprecatedAt.HasValue;
}

public sealed record FormDefinitionDetail(
    string Id,
    string Name,
    string? Description,
    string? Category,
    string SchemaJson,
    int Version,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long CreatedBy,
    long UpdatedBy,
    string? DataTableKey,
    string? Icon,
    DateTimeOffset? PublishedAt,
    long? PublishedBy,
    DateTimeOffset? DeprecatedAt = null)
{
    public bool IsDeprecated => DeprecatedAt.HasValue;
}

public sealed record FormDefinitionCreateRequest(
    string Name,
    string? Description,
    string? Category,
    string SchemaJson,
    string? DataTableKey,
    string? Icon);

public sealed record FormDefinitionUpdateRequest(
    string Name,
    string? Description,
    string? Category,
    string SchemaJson,
    string? DataTableKey,
    string? Icon);

public sealed record FormDefinitionSchemaUpdateRequest(
    string SchemaJson);

public sealed record FormDefinitionPublishRequest(
    string? Remark);

public sealed record FormDefinitionVersionListItem(
    string Id,
    string FormDefinitionId,
    int SnapshotVersion,
    string Name,
    string? Description,
    string? Category,
    string? DataTableKey,
    string? Icon,
    long CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record FormDefinitionVersionDetail(
    string Id,
    string FormDefinitionId,
    int SnapshotVersion,
    string Name,
    string? Description,
    string? Category,
    string SchemaJson,
    string? DataTableKey,
    string? Icon,
    long CreatedBy,
    DateTimeOffset CreatedAt);
