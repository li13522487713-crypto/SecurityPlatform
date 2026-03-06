using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流定义（包含节点、连线、配置的 JSON 模型）
/// </summary>
public sealed class ApprovalFlowDefinition : TenantEntity
{
    public ApprovalFlowDefinition()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        DefinitionJson = string.Empty;
    }

    public ApprovalFlowDefinition(TenantId tenantId, string name, string definitionJson, long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        DefinitionJson = definitionJson;
        Version = 1;
        Status = ApprovalFlowStatus.Draft;
        PublishedAt = null;
        PublishedByUserId = null;
    }

    /// <summary>流程名称</summary>
    public string Name { get; private set; }

    /// <summary>流程定义 JSON（节点+连线+布局+节点配置）</summary>
    public string DefinitionJson { get; private set; }

    /// <summary>版本号</summary>
    public int Version { get; private set; }

    /// <summary>状态</summary>
    public ApprovalFlowStatus Status { get; private set; }

    /// <summary>发布时间</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>发布人 ID</summary>
    public long? PublishedByUserId { get; private set; }

    /// <summary>可见范围配置 JSON（控制哪些用户/角色/部门可以看到该流程）</summary>
    /// <remarks>
    /// JSON格式示例：
    /// {
    ///   "scopeType": "All|Department|Role|User",
    ///   "departmentIds": [1, 2],
    ///   "roleCodes": ["Manager", "Admin"],
    ///   "userIds": [100, 200]
    /// }
    /// </remarks>
    public string? VisibilityScopeJson { get; private set; }

    /// <summary>流程分类（如：人事类、财务类、采购类等）</summary>
    public string? Category { get; private set; }

    /// <summary>流程描述/说明</summary>
    public string? Description { get; private set; }

    /// <summary>是否为快捷入口（系统推荐流程）</summary>
    public bool IsQuickEntry { get; private set; }

    public void Update(string name, string definitionJson, string? description = null, string? category = null, string? visibilityScopeJson = null)
    {
        Name = name;
        DefinitionJson = definitionJson;
        if (description != null)
        {
            Description = description;
        }
        if (category != null)
        {
            Category = category;
        }
        if (visibilityScopeJson != null)
        {
            VisibilityScopeJson = visibilityScopeJson;
        }
        Version += 1;
        if (Status != ApprovalFlowStatus.Draft)
        {
            Status = ApprovalFlowStatus.Draft;
        }
    }

    public void SetMetadata(string? description, string? category, string? visibilityScopeJson, bool? isQuickEntry = null)
    {
        if (description != null)
        {
            Description = description;
        }
        if (category != null)
        {
            Category = category;
        }
        if (visibilityScopeJson != null)
        {
            VisibilityScopeJson = visibilityScopeJson;
        }
        if (isQuickEntry.HasValue)
        {
            IsQuickEntry = isQuickEntry.Value;
        }
    }

    public void SetQuickEntry(bool isQuickEntry)
    {
        IsQuickEntry = isQuickEntry;
    }

    public void Publish(long publishedByUserId, DateTimeOffset now)
    {
        Status = ApprovalFlowStatus.Published;
        PublishedAt = now;
        PublishedByUserId = publishedByUserId;
    }

    public void Disable()
    {
        Status = ApprovalFlowStatus.Disabled;
    }

    public void Enable()
    {
        Status = ApprovalFlowStatus.Published;
    }

    /// <summary>标记为弃用状态：不允许新发起实例，但运行中实例可继续完成。</summary>
    public void Deprecate(long deprecatedByUserId, DateTimeOffset now)
    {
        DeprecatedAt = now;
        DeprecatedByUserId = deprecatedByUserId;
    }

    /// <summary>弃用时间（null 表示未弃用）</summary>
    public DateTimeOffset? DeprecatedAt { get; private set; }

    /// <summary>弃用人 ID</summary>
    public long? DeprecatedByUserId { get; private set; }

    /// <summary>是否已弃用</summary>
    public bool IsDeprecated => DeprecatedAt.HasValue;
}
