using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class ProjectUser : TenantEntity
{
    public ProjectUser()
        : base(TenantId.Empty)
    {
    }

    public ProjectUser(TenantId tenantId, long projectId, long userId, long id)
        : base(tenantId)
    {
        Id = id;
        ProjectId = projectId;
        UserId = userId;
    }

    public long ProjectId { get; private set; }
    public long UserId { get; private set; }
}
