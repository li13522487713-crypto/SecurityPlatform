using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 用户与外部 OIDC/SSO 提供者的绑定关系
/// </summary>
public sealed class OidcLink : TenantEntity
{
    public OidcLink() : base(TenantId.Empty)
    {
        ProviderId = string.Empty;
        ExternalSub = string.Empty;
    }

    public OidcLink(TenantId tenantId, long userId, string providerId, string externalSub, string? email, long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        ProviderId = providerId;
        ExternalSub = externalSub;
        Email = email;
        LinkedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>关联的本地用户 ID</summary>
    public long UserId { get; private set; }

    /// <summary>提供者唯一标识（与 OidcOptions.Providers[].ProviderId 对应）</summary>
    public string ProviderId { get; private set; }

    /// <summary>外部 IdP 的 subject claim（全局唯一 per provider）</summary>
    public string ExternalSub { get; private set; }

    /// <summary>首次绑定时记录的邮箱（可选）</summary>
    [SqlSugar.SugarColumn(IsNullable = true)]
    public string? Email { get; private set; }

    /// <summary>绑定时间</summary>
    public DateTimeOffset LinkedAt { get; private set; }

    /// <summary>最近一次通过此 IdP 登录的时间</summary>
    [SqlSugar.SugarColumn(IsNullable = true)]
    public DateTimeOffset? LastLoginAt { get; private set; }

    public void RecordLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
    }
}
