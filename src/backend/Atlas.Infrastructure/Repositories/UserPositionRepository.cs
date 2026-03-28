using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserPositionRepository : IUserPositionRepository
{
    private readonly ISqlSugarClient _db;

    public UserPositionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserPosition>> QueryByUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<UserPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<UserPosition>> QueryByUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<UserPosition>();
        }

        var ids = userIds.Distinct().ToArray();
        var list = await _db.Queryable<UserPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.UserId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByUserAndPositionIdsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> positionIds,
        CancellationToken cancellationToken)
    {
        if (positionIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var ids = positionIds.Distinct().ToArray();
        return _db.Deleteable<UserPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && SqlFunc.ContainsArray(ids, x.PositionId))
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByPositionIdAsync(TenantId tenantId, long positionId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PositionId == positionId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> ExistsByPositionIdAsync(TenantId tenantId, long positionId, CancellationToken cancellationToken)
    {
        var count = await _db.Queryable<UserPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PositionId == positionId)
            .CountAsync(cancellationToken);
        return count > 0;
    }

    public Task AddRangeAsync(IReadOnlyList<UserPosition> userPositions, CancellationToken cancellationToken)
    {
        if (userPositions.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(userPositions.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
