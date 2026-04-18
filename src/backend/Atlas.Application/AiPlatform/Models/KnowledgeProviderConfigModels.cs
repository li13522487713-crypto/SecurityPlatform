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
