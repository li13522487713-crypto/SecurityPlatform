using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

public static class WorkspaceBuiltInRoleCodes
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";
}

public static class WorkspacePermissionActions
{
    public const string View = "view";
    public const string Edit = "edit";
    public const string Publish = "publish";
    public const string Delete = "delete";
    public const string ManagePermission = "manage-permission";
}

public sealed class Workspace : TenantEntity
{
    public Workspace()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Icon = string.Empty;
        AppKey = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Workspace(
        TenantId tenantId,
        string name,
        string? description,
        string? icon,
        long appInstanceId,
        string appKey,
        long createdBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Icon = icon?.Trim() ?? string.Empty;
        AppInstanceId = appInstanceId;
        AppKey = appKey.Trim();
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedBy = createdBy;
        UpdatedAt = CreatedAt;
        IsArchived = false;
    }

    public string Name { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? Description { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? Icon { get; private set; }
    public long AppInstanceId { get; private set; }
    public string AppKey { get; private set; }
    public bool IsArchived { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? LastVisitedAt { get; private set; }

    public void Update(string name, string? description, string? icon, long updatedBy)
    {
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Icon = icon?.Trim() ?? string.Empty;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkVisited(long updatedBy)
    {
        LastVisitedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        UpdatedAt = LastVisitedAt;
    }

    public void Archive(long updatedBy)
    {
        IsArchived = true;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class WorkspaceRole : TenantEntity
{
    public WorkspaceRole()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        DefaultActionsJson = "[]";
        CreatedAt = DateTime.UtcNow;
    }

    public WorkspaceRole(
        TenantId tenantId,
        long workspaceId,
        string code,
        string name,
        string defaultActionsJson,
        bool isSystem,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Code = code.Trim();
        Name = name.Trim();
        DefaultActionsJson = string.IsNullOrWhiteSpace(defaultActionsJson) ? "[]" : defaultActionsJson;
        IsSystem = isSystem;
        CreatedAt = DateTime.UtcNow;
    }

    public long WorkspaceId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsSystem { get; private set; }
    public string DefaultActionsJson { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void UpdateActions(string defaultActionsJson)
    {
        DefaultActionsJson = string.IsNullOrWhiteSpace(defaultActionsJson) ? "[]" : defaultActionsJson;
    }
}

public sealed class WorkspaceMember : TenantEntity
{
    public WorkspaceMember()
        : base(TenantId.Empty)
    {
        JoinedAt = DateTime.UtcNow;
    }

    public WorkspaceMember(
        TenantId tenantId,
        long workspaceId,
        long userId,
        long workspaceRoleId,
        long addedBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        UserId = userId;
        WorkspaceRoleId = workspaceRoleId;
        AddedBy = addedBy;
        JoinedAt = DateTime.UtcNow;
        UpdatedAt = JoinedAt;
    }

    public long WorkspaceId { get; private set; }
    public long UserId { get; private set; }
    public long WorkspaceRoleId { get; private set; }
    public long AddedBy { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void ChangeRole(long workspaceRoleId)
    {
        WorkspaceRoleId = workspaceRoleId;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class WorkspaceResourcePermission : TenantEntity
{
    public WorkspaceResourcePermission()
        : base(TenantId.Empty)
    {
        ResourceType = string.Empty;
        ActionsJson = "[]";
        CreatedAt = DateTime.UtcNow;
    }

    public WorkspaceResourcePermission(
        TenantId tenantId,
        long workspaceId,
        long workspaceRoleId,
        string resourceType,
        long resourceId,
        string actionsJson,
        long updatedBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        WorkspaceRoleId = workspaceRoleId;
        ResourceType = resourceType.Trim().ToLowerInvariant();
        ResourceId = resourceId;
        ActionsJson = string.IsNullOrWhiteSpace(actionsJson) ? "[]" : actionsJson;
        UpdatedBy = updatedBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long WorkspaceId { get; private set; }
    public long WorkspaceRoleId { get; private set; }
    public string ResourceType { get; private set; }
    public long ResourceId { get; private set; }
    public string ActionsJson { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void UpdateActions(string actionsJson, long updatedBy)
    {
        ActionsJson = string.IsNullOrWhiteSpace(actionsJson) ? "[]" : actionsJson;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
