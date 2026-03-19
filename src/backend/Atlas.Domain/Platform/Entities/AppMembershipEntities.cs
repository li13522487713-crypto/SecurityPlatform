using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Platform.Entities;

public sealed class AppMember : TenantEntity
{
    public AppMember()
        : base(TenantId.Empty)
    {
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public AppMember(
        TenantId tenantId,
        long appId,
        long userId,
        long joinedBy,
        DateTimeOffset joinedAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        UserId = userId;
        JoinedBy = joinedBy;
        JoinedAt = joinedAt;
    }

    public long AppId { get; private set; }
    public long UserId { get; private set; }
    public long JoinedBy { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
}

public sealed class AppRole : TenantEntity
{
    public AppRole()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
    }

    public AppRole(
        TenantId tenantId,
        long appId,
        string code,
        string name,
        string? description,
        bool isSystem,
        long createdBy,
        DateTimeOffset createdAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Code = code;
        Name = name;
        Description = description ?? string.Empty;
        IsSystem = isSystem;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedBy = createdBy;
        UpdatedAt = createdAt;
    }

    public long AppId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool IsSystem { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        long updatedBy,
        DateTimeOffset updatedAt)
    {
        Name = name;
        Description = description ?? string.Empty;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }
}

public sealed class AppUserRole : TenantEntity
{
    public AppUserRole()
        : base(TenantId.Empty)
    {
    }

    public AppUserRole(
        TenantId tenantId,
        long appId,
        long userId,
        long roleId,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        UserId = userId;
        RoleId = roleId;
    }

    public long AppId { get; private set; }
    public long UserId { get; private set; }
    public long RoleId { get; private set; }
}

public sealed class AppRolePermission : TenantEntity
{
    public AppRolePermission()
        : base(TenantId.Empty)
    {
        PermissionCode = string.Empty;
    }

    public AppRolePermission(
        TenantId tenantId,
        long appId,
        long roleId,
        string permissionCode,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        RoleId = roleId;
        PermissionCode = permissionCode;
    }

    public long AppId { get; private set; }
    public long RoleId { get; private set; }
    public string PermissionCode { get; private set; }
}
