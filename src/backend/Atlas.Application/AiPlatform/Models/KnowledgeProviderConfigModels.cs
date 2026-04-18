namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgeProviderRole
{
    Upload = 0,
    Storage = 1,
    Vector = 2,
    Embedding = 3,
    Generation = 4
}

public enum KnowledgeProviderStatus
{
    Active = 0,
    Degraded = 1,
    Inactive = 2
}

public sealed record KnowledgeProviderConfigDto(
    string Id,
    KnowledgeProviderRole Role,
    string ProviderName,
    string DisplayName,
    KnowledgeProviderStatus Status,
    bool IsDefault,
    DateTime UpdatedAt,
    string? Endpoint = null,
    string? Region = null,
    string? BucketOrIndex = null,
    string? MetadataJson = null);

/// <summary>
/// Provider 配置写入请求（v5 §39 / 计划 G1+G5）。
/// admin 用：通过 PUT /provider-configs/{role} 更新或创建该 role 的默认 provider。
/// </summary>
public sealed record KnowledgeProviderConfigUpsertRequest(
    KnowledgeProviderRole Role,
    string ProviderName,
    string DisplayName,
    KnowledgeProviderStatus Status = KnowledgeProviderStatus.Active,
    bool IsDefault = true,
    string? Endpoint = null,
    string? Region = null,
    string? BucketOrIndex = null,
    string? MetadataJson = null);
