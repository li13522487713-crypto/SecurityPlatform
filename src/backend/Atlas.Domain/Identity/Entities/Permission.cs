using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class Permission : TenantEntity
{
    public Permission()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        Type = "Api";
        Description = string.Empty;
    }

    public Permission(TenantId tenantId, string name, string code, string type, long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Code = code;
        Type = type;
        Description = string.Empty;
    }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public string Type { get; private set; }
    public string? Description { get; private set; }

    public void Update(string name, string type, string? description)
    {
        Name = name;
        Type = type;
        Description = description ?? string.Empty;
    }
}
