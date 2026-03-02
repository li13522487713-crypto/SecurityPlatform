using Atlas.Core.Abstractions;
using Atlas.Core.Enums;
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
        DataScope = DataScopeType.CurrentTenant;
    }

    public Role(TenantId tenantId, string name, string code, long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Code = code;
        Description = string.Empty;
        IsSystem = false;
        DataScope = DataScopeType.CurrentTenant;
    }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }

    /// <summary>数据权限范围（等保2.0 最小化授权）</summary>
    public DataScopeType DataScope { get; private set; } = DataScopeType.CurrentTenant;

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description ?? string.Empty;
    }

    public void MarkSystemRole()
    {
        IsSystem = true;
    }

    public void SetDataScope(DataScopeType scope)
    {
        DataScope = scope;
    }
}
