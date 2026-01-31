using Atlas.Application.TableViews.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.TableViews.Abstractions;

public interface ITableViewQueryService
{
    Task<PagedResult<TableViewListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<TableViewDetail?> GetByIdAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken);

    Task<TableViewDetail?> GetDefaultAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<TableViewConfig> GetDefaultConfigAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken);
}
