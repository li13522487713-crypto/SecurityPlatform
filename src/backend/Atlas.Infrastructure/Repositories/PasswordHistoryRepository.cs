using Atlas.Application.Identity.Repositories;
using Atlas.Domain.Identity.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class PasswordHistoryRepository : IPasswordHistoryRepository
{
    private readonly ISqlSugarClient _db;

    public PasswordHistoryRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PasswordHistory>> GetRecentAsync(
        TenantId tenantId,
        long userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<PasswordHistory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return list;
    }

    public Task AddAsync(PasswordHistory history, CancellationToken cancellationToken)
        => _db.Insertable(history).ExecuteCommandAsync(cancellationToken);

    public async Task DeleteExceptRecentAsync(
        TenantId tenantId,
        long userId,
        int keep,
        CancellationToken cancellationToken)
    {
        if (keep <= 0)
        {
            return;
        }

        var idsToRemove = await _db.Queryable<PasswordHistory>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Skip(keep)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (idsToRemove.Count == 0)
        {
            return;
        }

        var idArray = idsToRemove.Distinct().ToArray();
        await _db.Deleteable<PasswordHistory>()
            .Where(x => SqlFunc.ContainsArray(idArray, x.Id))
            .ExecuteCommandAsync(cancellationToken);
    }
}
