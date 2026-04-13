using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiDatabaseBinding : TenantEntity
{
    public AiDatabaseBinding() : base(TenantId.Empty)
    {
        OwnerType = string.Empty;
        ConfigJson = "{}";
    }

    public string OwnerType { get; private set; }
    public long OwnerId { get; private set; }
    public long DatabaseId { get; private set; }
    public string ConfigJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
