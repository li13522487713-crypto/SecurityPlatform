using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IPermissionQueryService
{
    Task<PagedResult<PermissionListItem>> QueryPermissionsAsync(
        PermissionQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<PermissionDetail?> GetDetailAsync(
        long id,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
