using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 外部用户镜像。一行 = 一个外部账号。绑定关系另由 ExternalIdentityBinding 表达。
/// 唯一键：(TenantId, ProviderId, ExternalUserId)。
/// </summary>
public sealed class ExternalUserMirror : TenantEntity
{
    public ExternalUserMirror()
        : base(TenantId.Empty)
    {
        ExternalUserId = string.Empty;
    }

    public ExternalUserMirror(
        TenantId tenantId,
        long id,
        long providerId,
        string externalUserId,
        string? openId,
        string? unionId,
        string? name,
        string? englishName,
        string? mobile,
        string? email,
        string? avatar,
        string? position,
        string? primaryDepartmentId,
        string? status,
        string? rawJson,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        ExternalUserId = externalUserId;
        OpenId = openId;
        UnionId = unionId;
        Name = name;
        EnglishName = englishName;
        Mobile = mobile;
        Email = email;
        Avatar = avatar;
        Position = position;
        PrimaryDepartmentId = primaryDepartmentId;
        Status = status;
        RawJson = rawJson;
        IsDeleted = false;
        FirstSeenAt = now;
        LastSyncedAt = now;
    }

    public long ProviderId { get; private set; }

    public string ExternalUserId { get; private set; }

    public string? OpenId { get; private set; }

    public string? UnionId { get; private set; }

    public string? Name { get; private set; }

    public string? EnglishName { get; private set; }

    public string? Mobile { get; private set; }

    public string? Email { get; private set; }

    public string? Avatar { get; private set; }

    public string? Position { get; private set; }

    public string? PrimaryDepartmentId { get; private set; }

    public string? Status { get; private set; }

    public string? RawJson { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSyncedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public void UpdateFrom(
        string? openId,
        string? unionId,
        string? name,
        string? englishName,
        string? mobile,
        string? email,
        string? avatar,
        string? position,
        string? primaryDepartmentId,
        string? status,
        string? rawJson,
        DateTimeOffset now)
    {
        OpenId = openId;
        UnionId = unionId;
        Name = name;
        EnglishName = englishName;
        Mobile = mobile;
        Email = email;
        Avatar = avatar;
        Position = position;
        PrimaryDepartmentId = primaryDepartmentId;
        Status = status;
        RawJson = rawJson;
        IsDeleted = false;
        DeletedAt = null;
        LastSyncedAt = now;
    }

    public void MarkDeleted(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
        LastSyncedAt = now;
    }
}
