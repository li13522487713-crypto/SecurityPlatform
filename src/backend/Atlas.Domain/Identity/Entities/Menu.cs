using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class Menu : TenantEntity
{
    public Menu()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Path = string.Empty;
        Component = string.Empty;
        Icon = string.Empty;
        ParentId = 0;
        SortOrder = 0;
        MenuType = "C";
        Perms = string.Empty;
        Query = string.Empty;
        IsFrame = false;
        IsCache = false;
        Visible = "0";
        Status = "0";
        PermissionCode = string.Empty;
        IsHidden = false;
    }

    public Menu(
        TenantId tenantId,
        string name,
        string path,
        long id,
        long? parentId,
        int sortOrder,
        string? menuType,
        string? component,
        string? icon,
        string? perms,
        string? query,
        bool isFrame,
        bool isCache,
        string? visible,
        string? status,
        string? permissionCode,
        bool isHidden)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Path = path;
        ParentId = parentId ?? 0;
        SortOrder = sortOrder;
        MenuType = string.IsNullOrWhiteSpace(menuType) ? "C" : menuType;
        Component = component ?? string.Empty;
        Icon = icon ?? string.Empty;
        Perms = perms ?? permissionCode ?? string.Empty;
        Query = query ?? string.Empty;
        IsFrame = isFrame;
        IsCache = isCache;
        Visible = string.IsNullOrWhiteSpace(visible) ? (isHidden ? "1" : "0") : visible;
        Status = string.IsNullOrWhiteSpace(status) ? "0" : status;
        PermissionCode = permissionCode ?? perms ?? string.Empty;
        IsHidden = isHidden;
    }

    public string Name { get; private set; }
    public string Path { get; private set; }
    public string? Component { get; private set; }
    public string? Icon { get; private set; }
    public long? ParentId { get; private set; }
    public int SortOrder { get; private set; }
    public string MenuType { get; private set; }
    public string? Perms { get; private set; }
    public string? Query { get; private set; }
    public bool IsFrame { get; private set; }
    public bool IsCache { get; private set; }
    public string Visible { get; private set; }
    public string Status { get; private set; }
    public string? PermissionCode { get; private set; }
    public bool IsHidden { get; private set; }

    public void Update(
        string name,
        string path,
        long? parentId,
        int sortOrder,
        string? menuType,
        string? component,
        string? icon,
        string? perms,
        string? query,
        bool isFrame,
        bool isCache,
        string? visible,
        string? status,
        string? permissionCode,
        bool isHidden)
    {
        Name = name;
        Path = path;
        ParentId = parentId ?? 0;
        SortOrder = sortOrder;
        MenuType = string.IsNullOrWhiteSpace(menuType) ? "C" : menuType;
        Component = component ?? string.Empty;
        Icon = icon ?? string.Empty;
        Perms = perms ?? permissionCode ?? string.Empty;
        Query = query ?? string.Empty;
        IsFrame = isFrame;
        IsCache = isCache;
        Visible = string.IsNullOrWhiteSpace(visible) ? (isHidden ? "1" : "0") : visible;
        Status = string.IsNullOrWhiteSpace(status) ? "0" : status;
        PermissionCode = permissionCode ?? perms ?? string.Empty;
        IsHidden = isHidden;
    }

    public void UpdateSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }
}
