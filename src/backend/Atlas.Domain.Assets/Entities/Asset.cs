using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Assets.Entities;

public sealed class Asset : TenantEntity
{
    public Asset()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
    }

    public Asset(TenantId tenantId, string name)
        : base(tenantId)
    {
        Name = name;
    }

    public Asset(TenantId tenantId, string name, long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
    }

    public string Name { get; private set; }
}