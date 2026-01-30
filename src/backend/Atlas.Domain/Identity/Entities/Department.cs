using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class Department : TenantEntity
{
    public Department()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        ParentId = 0;
        SortOrder = 0;
    }

    public Department(TenantId tenantId, string name, long id, long? parentId, int sortOrder)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        ParentId = parentId ?? 0;
        SortOrder = sortOrder;
    }

    public string Name { get; private set; }
    public long? ParentId { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string name, long? parentId, int sortOrder)
    {
        Name = name;
        ParentId = parentId ?? 0;
        SortOrder = sortOrder;
    }
}
