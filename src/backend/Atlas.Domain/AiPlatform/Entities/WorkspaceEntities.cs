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
        AppKey = null;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Workspace(
        TenantId tenantId,
        string name,
        string? description,
        string? icon,
        long createdBy,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Icon = icon?.Trim() ?? string.Empty;
        AppInstanceId = null;
        AppKey = null;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedBy = createdBy;
        UpdatedAt = CreatedAt;
        IsArchived = false;
    }

    [Obsolete("旧的一对一绑定构造已废弃；请使用无 AppInstance/AppKey 的构造，应用实例通过 AssignWorkspace 反向关联。仅保留给历史 Coze/迁移路径使用。")]
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
        AppInstanceId = appInstanceId > 0 ? appInstanceId : null;
        AppKey = string.IsNullOrWhiteSpace(appKey) ? null : appKey.Trim();
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
    /// <summary>
    /// 历史「主应用实例」绑定字段。1→N 模型下保留给前端兼容（详情接口会回填默认 manifest）。
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public long? AppInstanceId { get; private set; }
    /// <summary>
    /// 历史「主应用 Key」绑定字段。1→N 模型下保留给前端兼容（详情接口会回填默认 manifest 的 AppKey）。
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public string? AppKey { get; private set; }
    public bool IsArchived { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? LastVisitedAt { get; private set; }

    /// <summary>
    /// 治理 M-G05-C2（S9）：所属组织。当前 nullable，S10 阶段做完数据回填后转 non-null。
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public long? OrganizationId { get; private set; }

    public void AssignOrganization(long organizationId)
    {
        if (organizationId > 0)
        {
            OrganizationId = organizationId;
        }
    }

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

    /// <summary>
    /// 把某个应用实例（AppManifest）绑定为本工作空间的「默认/主应用」。
    /// 仅用于回填历史 AppInstanceId/AppKey 字段以保持前端兼容；真正的 1→N 关系由 AppManifest.WorkspaceId 维护。
    /// </summary>
    public void BindDefaultAppInstance(long appInstanceId, string appKey, long updatedBy)
    {
        if (appInstanceId <= 0)
        {
            return;
        }

        AppInstanceId = appInstanceId;
        AppKey = string.IsNullOrWhiteSpace(appKey) ? null : appKey.Trim();
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
