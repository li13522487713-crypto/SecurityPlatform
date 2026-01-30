using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IMenuRepository
{
    Task<Menu?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Menu> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Menu>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Menu>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);
    Task AddAsync(Menu menu, CancellationToken cancellationToken);
    Task UpdateAsync(Menu menu, CancellationToken cancellationToken);
}
