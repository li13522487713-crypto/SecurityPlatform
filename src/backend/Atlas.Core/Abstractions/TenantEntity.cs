using Atlas.Core.Tenancy;

namespace Atlas.Core.Abstractions;

public abstract class TenantEntity : EntityBase, ITenantScoped
{
    protected TenantEntity(TenantId tenantId)
    {
        TenantIdValue = tenantId.Value;
    }

    public Guid TenantIdValue { get; protected set; }

    public TenantId TenantId => new(TenantIdValue);
}