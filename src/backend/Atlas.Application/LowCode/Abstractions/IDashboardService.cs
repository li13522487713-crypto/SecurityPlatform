using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IDashboardService
{
    Task<PagedResult<DashboardDefinitionListItem>> QueryAsync(PagedRequest request, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<DashboardDefinitionDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(TenantId tenantId, long userId, DashboardDefinitionCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long userId, long id, DashboardDefinitionUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
}
