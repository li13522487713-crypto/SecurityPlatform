using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class Role : TenantEntity
{
    public Role()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        Description = string.Empty;
        IsSystem = false;
    }

    public Role(TenantId tenantId, string name, string code, long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Code = code;
        Description = string.Empty;
        IsSystem = false;
    }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description ?? string.Empty;
    }

    public void MarkSystemRole()
    {
        IsSystem = true;
    }
}
