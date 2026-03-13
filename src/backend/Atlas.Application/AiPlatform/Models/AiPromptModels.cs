namespace Atlas.Application.AiPlatform.Models;

public sealed record AiPromptTemplateListItem(
    long Id,
    string Name,
    string? Description,
    string? Category,
    string Content,
    IReadOnlyList<string> Tags,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiPromptTemplateDetail(
    long Id,
    string Name,
    string? Description,
    string? Category,
    string Content,
    IReadOnlyList<string> Tags,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiPromptTemplateCreateRequest(
    string Name,
    string? Description,
    string? Category,
    string Content,
    IReadOnlyList<string>? Tags,
    bool IsSystem);

public sealed record AiPromptTemplateUpdateRequest(
    string Name,
    string? Description,
    string? Category,
    string Content,
    IReadOnlyList<string>? Tags);
