namespace Atlas.Application.LowCode.Models;

public sealed record LowCodeEnvironmentListItem(
    string Id,
    string AppId,
    string Name,
    string Code,
    string? Description,
    bool IsDefault,
    bool IsActive,
    DateTimeOffset UpdatedAt);

public sealed record LowCodeEnvironmentDetail(
    string Id,
    string AppId,
    string Name,
    string Code,
    string? Description,
    bool IsDefault,
    bool IsActive,
    string VariablesJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long CreatedBy,
    long UpdatedBy);

public sealed record LowCodeEnvironmentCreateRequest(
    string Name,
    string Code,
    string? Description,
    bool IsDefault,
    string VariablesJson);

public sealed record LowCodeEnvironmentUpdateRequest(
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive,
    string VariablesJson);
