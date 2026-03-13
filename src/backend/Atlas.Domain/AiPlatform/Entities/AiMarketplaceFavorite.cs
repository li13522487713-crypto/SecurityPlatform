using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiMarketplaceFavorite : TenantEntity
{
    public AiMarketplaceFavorite()
        : base(TenantId.Empty)
    {
        CreatedAt = DateTime.UtcNow;
    }

    public AiMarketplaceFavorite(
        TenantId tenantId,
        long productId,
        long userId,
        long id)
        : base(tenantId)
    {
        Id = id;
        ProductId = productId;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
    }

    public long ProductId { get; private set; }
    public long UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
