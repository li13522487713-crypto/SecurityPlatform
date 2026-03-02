using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码页面定义（存储 amis JSON schema + 路由配置）
/// </summary>
public sealed class LowCodePage : TenantEntity
{
    public LowCodePage()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        PageKey = string.Empty;
        SchemaJson = string.Empty;
    }

    public LowCodePage(
        TenantId tenantId,
        long appId,
        string pageKey,
        string name,
        LowCodePageType pageType,
        string schemaJson,
        string? routePath,
        string? description,
        string? icon,
        int sortOrder,
        long? parentPageId,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        PageKey = pageKey;
        Name = name;
        PageType = pageType;
        SchemaJson = schemaJson;
        RoutePath = routePath;
        Description = description;
        Icon = icon;
        SortOrder = sortOrder;
        ParentPageId = parentPageId;
        Version = 1;
        IsPublished = false;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    /// <summary>所属应用 ID</summary>
    public long AppId { get; private set; }

    /// <summary>页面唯一标识</summary>
    public string PageKey { get; private set; }

    /// <summary>页面名称</summary>
    public string Name { get; private set; }

    /// <summary>页面类型</summary>
    public LowCodePageType PageType { get; private set; }

    /// <summary>amis JSON Schema</summary>
    public string SchemaJson { get; private set; }

    /// <summary>路由路径（如 /app/crm/customer-list）</summary>
    public string? RoutePath { get; private set; }

    /// <summary>页面描述</summary>
    public string? Description { get; private set; }

    /// <summary>图标</summary>
    public string? Icon { get; private set; }

    /// <summary>排序</summary>
    public int SortOrder { get; private set; }

    /// <summary>父页面 ID（用于主子页面关系）</summary>
    public long? ParentPageId { get; private set; }

    /// <summary>版本号</summary>
    public int Version { get; private set; }

    /// <summary>是否已发布</summary>
    public bool IsPublished { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>创建人 ID</summary>
    public long CreatedBy { get; private set; }

    /// <summary>更新人 ID</summary>
    public long UpdatedBy { get; private set; }

    /// <summary>权限编码（控制页面访问权限）</summary>
    public string? PermissionCode { get; private set; }

    /// <summary>关联的数据表 Key</summary>
    public string? DataTableKey { get; private set; }

    public void Update(
        string name,
        LowCodePageType pageType,
        string schemaJson,
        string? routePath,
        string? description,
        string? icon,
        int sortOrder,
        long? parentPageId,
        long updatedBy,
        DateTimeOffset now)
    {
        Name = name;
        PageType = pageType;
        SchemaJson = schemaJson;
        RoutePath = routePath;
        Description = description;
        Icon = icon;
        SortOrder = sortOrder;
        ParentPageId = parentPageId;
        Version += 1;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void UpdateSchema(string schemaJson, long updatedBy, DateTimeOffset now)
    {
        SchemaJson = schemaJson;
        Version += 1;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Publish(long updatedBy, DateTimeOffset now)
    {
        IsPublished = true;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Unpublish(long updatedBy, DateTimeOffset now)
    {
        IsPublished = false;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void SetPermission(string? permissionCode, long updatedBy, DateTimeOffset now)
    {
        PermissionCode = permissionCode;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void BindDataTable(string? dataTableKey, long updatedBy, DateTimeOffset now)
    {
        DataTableKey = dataTableKey;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
