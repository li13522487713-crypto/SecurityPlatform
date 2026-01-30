using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public sealed class Position : TenantEntity
{
    public Position()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        Description = string.Empty;
        IsActive = true;
        IsSystem = false;
        SortOrder = 0;
    }

    public Position(TenantId tenantId, string name, string code, long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Code = code;
        Description = string.Empty;
        IsActive = true;
        IsSystem = false;
        SortOrder = 0;
    }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSystem { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string name, string? description, bool isActive, int sortOrder)
    {
        Name = name;
        Description = description ?? string.Empty;
        IsActive = isActive;
        SortOrder = sortOrder;
    }

    public void MarkSystem()
    {
        IsSystem = true;
    }
}
