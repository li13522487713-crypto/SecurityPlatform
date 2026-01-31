using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class ProjectDepartment : TenantEntity
{
    public ProjectDepartment()
        : base(TenantId.Empty)
    {
    }

    public ProjectDepartment(TenantId tenantId, long projectId, long departmentId, long id)
        : base(tenantId)
    {
        Id = id;
        ProjectId = projectId;
        DepartmentId = departmentId;
    }

    public long ProjectId { get; private set; }
    public long DepartmentId { get; private set; }
}
