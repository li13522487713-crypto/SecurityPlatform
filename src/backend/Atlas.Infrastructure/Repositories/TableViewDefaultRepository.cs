using Atlas.Application.TableViews.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class TableViewDefaultRepository : ITableViewDefaultRepository
{
    private readonly ISqlSugarClient _db;

    public TableViewDefaultRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<UserTableViewDefault?> FindByTableKeyAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<UserTableViewDefault>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.TableKey == tableKey)
            .FirstAsync(cancellationToken);
    }

    public Task AddAsync(UserTableViewDefault entry, CancellationToken cancellationToken)
    {
        return _db.Insertable(entry).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(UserTableViewDefault entry, CancellationToken cancellationToken)
    {
        return _db.Updateable(entry)
            .Where(x => x.Id == entry.Id && x.TenantIdValue == entry.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByViewIdAsync(TenantId tenantId, long userId, long viewId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserTableViewDefault>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && x.ViewId == viewId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
