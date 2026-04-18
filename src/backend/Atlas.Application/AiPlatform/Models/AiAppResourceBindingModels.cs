namespace Atlas.Application.AiPlatform.Models;

/// <summary>App 资源绑定（K5）。统一描述 App 与 KB / DB / 其他资源的关联。</summary>
public sealed record AiAppResourceBindingDto(
    long Id,
    long AppId,
    string ResourceType,
    long ResourceId,
    string Role,
    int DisplayOrder,
    string ConfigJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiAppResourceBindingCreateRequest(
    string ResourceType,
    long ResourceId,
    string? Role = null,
    int DisplayOrder = 0,
    string? ConfigJson = null);

public sealed record AiAppResourceBindingUpdateRequest(
    string? Role = null,
    int? DisplayOrder = null,
    string? ConfigJson = null);
