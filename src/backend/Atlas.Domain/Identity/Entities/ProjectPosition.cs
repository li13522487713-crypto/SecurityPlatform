using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class ProjectPosition : TenantEntity
{
    public ProjectPosition()
        : base(TenantId.Empty)
    {
    }

    public ProjectPosition(TenantId tenantId, long projectId, long positionId, long id)
        : base(tenantId)
    {
        Id = id;
        ProjectId = projectId;
        PositionId = positionId;
    }

    public long ProjectId { get; private set; }
    public long PositionId { get; private set; }
}
