using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 页面发布版本快照，用于历史追溯与回滚。
/// </summary>
public sealed class LowCodePageVersion : TenantEntity
{
    public LowCodePageVersion()
        : base(TenantId.Empty)
    {
        PageKey = string.Empty;
        Name = string.Empty;
        SchemaJson = string.Empty;
    }

    public LowCodePageVersion(
        TenantId tenantId,
        long pageId,
        long appId,
        int snapshotVersion,
        string pageKey,
        string name,
        LowCodePageType pageType,
        string schemaJson,
        string? routePath,
        string? description,
        string? icon,
        int sortOrder,
        long? parentPageId,
        string? permissionCode,
        string? dataTableKey,
        long createdBy,
        long id,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        PageId = pageId;
        AppId = appId;
        SnapshotVersion = snapshotVersion;
        PageKey = pageKey;
        Name = name;
        PageType = pageType;
        SchemaJson = schemaJson;
        RoutePath = routePath;
        Description = description;
        Icon = icon;
        SortOrder = sortOrder;
        ParentPageId = parentPageId;
        PermissionCode = permissionCode;
        DataTableKey = dataTableKey;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public long PageId { get; private set; }
    public long AppId { get; private set; }
    public int SnapshotVersion { get; private set; }
    public string PageKey { get; private set; }
    public string Name { get; private set; }
    public LowCodePageType PageType { get; private set; }
    public string SchemaJson { get; private set; }
    public string? RoutePath { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public int SortOrder { get; private set; }
    public long? ParentPageId { get; private set; }
    public string? PermissionCode { get; private set; }
    public string? DataTableKey { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
