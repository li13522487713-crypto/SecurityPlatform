using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class Project : TenantEntity
{
    public Project()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        IsActive = true;
        SortOrder = 0;
    }

    public Project(TenantId tenantId, string code, string name, long id)
        : base(tenantId)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = string.Empty;
        IsActive = true;
        SortOrder = 0;
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
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
