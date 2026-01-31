using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IRoleQueryService
{
    Task<PagedResult<RoleListItem>> QueryRolesAsync(
        RoleQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<RoleDetail?> GetDetailAsync(
        long id,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
