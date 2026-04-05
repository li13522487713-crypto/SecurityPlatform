using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Platform.Entities;

/// <summary>
/// 应用级部门（隶属于某个具体应用，与平台级 Department 相互隔离）。
/// </summary>
public sealed class AppDepartment : TenantEntity
{
    public AppDepartment()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        SortOrder = 0;
    }

    public AppDepartment(
        TenantId tenantId,
        long appId,
        string name,
        string code,
        long? parentId,
        int sortOrder,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Name = name;
        Code = code;
        ParentId = parentId;
        SortOrder = sortOrder;
    }

    public long AppId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public long? ParentId { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string name, string code, long? parentId, int sortOrder)
    {
        Name = name;
        Code = code;
        ParentId = parentId;
        SortOrder = sortOrder;
    }
}

/// <summary>
/// 应用级职位（隶属于某个具体应用，与平台级 Position 相互隔离）。
/// </summary>
public sealed class AppPosition : TenantEntity
{
    public AppPosition()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        Description = string.Empty;
        IsActive = true;
        SortOrder = 0;
    }

    public AppPosition(
        TenantId tenantId,
        long appId,
        string name,
        string code,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Name = name;
        Code = code;
        Description = string.Empty;
        IsActive = true;
        SortOrder = 0;
    }

    public long AppId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string name, string? description, bool isActive, int sortOrder)
    {
        Name = name;
        Description = description ?? string.Empty;
        IsActive = isActive;
        SortOrder = sortOrder;
    }
}

/// <summary>
/// 应用级项目（隶属于某个具体应用，与平台级 Project 相互隔离）。
/// </summary>
public sealed class AppProject : TenantEntity
{
    public AppProject()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        IsActive = true;
    }

    public AppProject(
        TenantId tenantId,
        long appId,
        string code,
        string name,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Code = code;
        Name = name;
        Description = string.Empty;
        IsActive = true;
    }

    public long AppId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string? description, bool isActive)
    {
        Name = name;
        Description = description ?? string.Empty;
        IsActive = isActive;
    }
}

/// <summary>
/// 应用成员 ↔ 应用项目关联（对应平台级的 ProjectUser）。
/// </summary>
public sealed class AppProjectUser : TenantEntity
{
    public AppProjectUser()
        : base(TenantId.Empty)
    {
    }

    public AppProjectUser(
        TenantId tenantId,
        long appId,
        long projectId,
        long userId,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        ProjectId = projectId;
        UserId = userId;
    }

    public long AppId { get; private set; }
    public long ProjectId { get; private set; }
    public long UserId { get; private set; }
}
