using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public sealed class UserPosition : TenantEntity
{
    public UserPosition()
        : base(TenantId.Empty)
    {
        IsPrimary = false;
    }

    public UserPosition(TenantId tenantId, long userId, long positionId, long id, bool isPrimary)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        PositionId = positionId;
        IsPrimary = isPrimary;
    }

    public long UserId { get; private set; }
    public long PositionId { get; private set; }
    public bool IsPrimary { get; private set; }
}
