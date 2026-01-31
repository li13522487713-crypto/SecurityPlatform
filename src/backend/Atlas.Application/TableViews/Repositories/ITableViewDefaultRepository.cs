using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.TableViews.Repositories;

public interface ITableViewDefaultRepository
{
    Task<UserTableViewDefault?> FindByTableKeyAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task AddAsync(UserTableViewDefault entry, CancellationToken cancellationToken);
    Task UpdateAsync(UserTableViewDefault entry, CancellationToken cancellationToken);
    Task DeleteByViewIdAsync(TenantId tenantId, long userId, long viewId, CancellationToken cancellationToken);
}
