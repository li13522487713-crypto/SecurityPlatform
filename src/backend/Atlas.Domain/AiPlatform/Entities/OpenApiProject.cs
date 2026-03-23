using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class OpenApiProject : TenantEntity
{
    public OpenApiProject()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        AppId = string.Empty;
        SecretPrefix = string.Empty;
        SecretHash = string.Empty;
        ScopesJson = "[]";
        IsActive = true;
        ExpiresAt = DateTime.UnixEpoch;
        LastUsedAt = DateTime.UnixEpoch;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public OpenApiProject(
        TenantId tenantId,
        string name,
        string? description,
        string appId,
        string secretPrefix,
        string secretHash,
        string scopesJson,
        long createdByUserId,
        DateTime? expiresAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        AppId = appId;
        SecretPrefix = secretPrefix;
        SecretHash = secretHash;
        ScopesJson = scopesJson;
        CreatedByUserId = createdByUserId;
        ExpiresAt = expiresAt ?? DateTime.UnixEpoch;
        LastUsedAt = DateTime.UnixEpoch;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public string AppId { get; private set; }
    public string SecretPrefix { get; private set; }
    public string SecretHash { get; private set; }
    public string ScopesJson { get; private set; }
    public long CreatedByUserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime LastUsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        string scopesJson,
        bool isActive,
        DateTime? expiresAt)
    {
        Name = name;
        Description = description ?? string.Empty;
        ScopesJson = scopesJson;
        IsActive = isActive;
        ExpiresAt = expiresAt ?? DateTime.UnixEpoch;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateSecret(string secretPrefix, string secretHash)
    {
        SecretPrefix = secretPrefix;
        SecretHash = secretHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
