using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.TableViews.Repositories;

public interface ITableViewRepository
{
    Task<TableView?> FindByIdAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken);

    Task<TableView?> FindByNameAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        string name,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<TableView> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);

    Task AddAsync(TableView view, CancellationToken cancellationToken);
    Task UpdateAsync(TableView view, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken);
}
