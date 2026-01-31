using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class AppConfig : TenantEntity
{
    public AppConfig()
        : base(TenantId.Empty)
    {
        AppId = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        IsActive = true;
        EnableProjectScope = false;
        SortOrder = 0;
    }

    public AppConfig(TenantId tenantId, string appId, string name, long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Name = name;
        Description = string.Empty;
        IsActive = true;
        EnableProjectScope = false;
        SortOrder = 0;
    }

    public string AppId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public bool EnableProjectScope { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string name, bool isActive, bool enableProjectScope, string? description, int sortOrder)
    {
        Name = name;
        IsActive = isActive;
        EnableProjectScope = enableProjectScope;
        Description = description ?? string.Empty;
        SortOrder = sortOrder;
    }
}
