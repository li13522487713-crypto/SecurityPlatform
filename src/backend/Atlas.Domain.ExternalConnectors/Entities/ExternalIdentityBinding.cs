using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 本地用户与外部身份的绑定关系。一个本地用户可以同时绑定多家 provider，但同一 (Provider, ExternalUserId) 在租户内唯一。
/// </summary>
public sealed class ExternalIdentityBinding : TenantEntity
{
    public ExternalIdentityBinding()
        : base(TenantId.Empty)
    {
        ExternalUserId = string.Empty;
        Source = string.Empty;
    }

    public ExternalIdentityBinding(
        TenantId tenantId,
        long id,
        long providerId,
        long localUserId,
        string externalUserId,
        string? openId,
        string? unionId,
        string? mobile,
        string? email,
        IdentityBindingMatchStrategy strategy,
        string source,
        DateTimeOffset now,
        IdentityBindingStatus status = IdentityBindingStatus.Active)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        LocalUserId = localUserId;
        ExternalUserId = externalUserId;
        OpenId = openId;
        UnionId = unionId;
        Mobile = mobile;
        Email = email;
        MatchStrategy = strategy;
        Source = source;
        Status = status;
        BoundAt = now;
        UpdatedAt = now;
    }

    public long ProviderId { get; private set; }

    public long LocalUserId { get; private set; }

    public string ExternalUserId { get; private set; }

    public string? OpenId { get; private set; }

    public string? UnionId { get; private set; }

    public string? Mobile { get; private set; }

    public string? Email { get; private set; }

    public IdentityBindingStatus Status { get; private set; }

    public IdentityBindingMatchStrategy MatchStrategy { get; private set; }

    /// <summary>触发该绑定的来源（"oauth_callback" / "manual_admin" / "directory_sync" / "import"）。</summary>
    public string Source { get; private set; }

    public DateTimeOffset BoundAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public void UpdateProfileSnapshot(string? openId, string? unionId, string? mobile, string? email, DateTimeOffset now)
    {
        OpenId = openId;
        UnionId = unionId;
        Mobile = mobile;
        Email = email;
        UpdatedAt = now;
    }

    public void Confirm(DateTimeOffset now)
    {
        Status = IdentityBindingStatus.Active;
        UpdatedAt = now;
    }

    public void MarkConflict(DateTimeOffset now)
    {
        Status = IdentityBindingStatus.Conflict;
        UpdatedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        Status = IdentityBindingStatus.Revoked;
        RevokedAt = now;
        UpdatedAt = now;
    }

    public void TouchLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
        UpdatedAt = now;
    }
}
