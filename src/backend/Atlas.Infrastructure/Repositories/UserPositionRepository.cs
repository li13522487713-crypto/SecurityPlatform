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

    public Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
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
