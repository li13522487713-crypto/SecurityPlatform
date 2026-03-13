using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class PersonalAccessToken : TenantEntity
{
    public PersonalAccessToken()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        TokenPrefix = string.Empty;
        TokenHash = string.Empty;
        ScopesJson = "[]";
        CreatedAt = DateTime.UtcNow;
    }

    public PersonalAccessToken(
        TenantId tenantId,
        string name,
        string tokenPrefix,
        string tokenHash,
        string scopesJson,
        long createdByUserId,
        DateTimeOffset? expiresAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        TokenPrefix = tokenPrefix;
        TokenHash = tokenHash;
        ScopesJson = scopesJson;
        CreatedByUserId = createdByUserId;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string TokenPrefix { get; private set; }
    public string TokenHash { get; private set; }
    public string ScopesJson { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(string name, string scopesJson, DateTimeOffset? expiresAt)
    {
        Name = name;
        ScopesJson = scopesJson;
        ExpiresAt = expiresAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        RevokedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
