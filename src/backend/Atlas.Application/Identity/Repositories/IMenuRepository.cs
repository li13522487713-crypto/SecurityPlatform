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
        bool? isHidden,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Menu>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Menu>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);
    Task AddAsync(Menu menu, CancellationToken cancellationToken);
    Task UpdateAsync(Menu menu, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long menuId, CancellationToken cancellationToken);
    Task<bool> HasChildrenAsync(TenantId tenantId, long menuId, CancellationToken cancellationToken);
    Task<bool> ExistsByPathAsync(TenantId tenantId, string path, long? excludeMenuId, CancellationToken cancellationToken);
    Task BatchUpdateSortOrderAsync(TenantId tenantId, IReadOnlyList<(long MenuId, int SortOrder)> updates, CancellationToken cancellationToken);
}
