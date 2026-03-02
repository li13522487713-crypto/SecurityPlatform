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
    DateTimeOffset? PublishedAt);

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
    long? PublishedBy);

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
