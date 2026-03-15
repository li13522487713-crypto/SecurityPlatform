using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class Department : TenantEntity
{
    public Department()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Code = string.Empty;
        ParentId = 0;
        SortOrder = 0;
    }

    public Department(TenantId tenantId, string name, string code, long id, long? parentId, int sortOrder)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Code = code;
        ParentId = parentId ?? 0;
        SortOrder = sortOrder;
    }

    public string Name { get; private set; }
    public string Code { get; private set; }
    public long? ParentId { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string name, string code, long? parentId, int sortOrder)
    {
        Name = name;
        Code = code;
        ParentId = parentId ?? 0;
        SortOrder = sortOrder;
    }
}
