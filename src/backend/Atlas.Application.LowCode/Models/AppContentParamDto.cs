namespace Atlas.Application.LowCode.Models;

/// <summary>内容参数 DTO（6 类）。</summary>
public sealed record AppContentParamDto(
    string Id,
    string AppId,
    string Code,
    string Kind,
    string ConfigJson,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AppContentParamCreateRequest(
    string Code,
    string Kind,
    string ConfigJson,
    string? Description);

public sealed record AppContentParamUpdateRequest(
    string Kind,
    string ConfigJson,
    string? Description);
