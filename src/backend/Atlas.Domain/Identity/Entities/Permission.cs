using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

/// <summary>平台级权限（应用级权限已物理分离至 AppPermission 实体）</summary>
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
