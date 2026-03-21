using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IMenuCommandService
{
    Task<long> CreateAsync(TenantId tenantId, MenuCreateRequest request, long id, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long menuId, MenuUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long menuId, CancellationToken cancellationToken);
    Task BatchSortAsync(TenantId tenantId, MenuBatchSortRequest request, CancellationToken cancellationToken);
}
