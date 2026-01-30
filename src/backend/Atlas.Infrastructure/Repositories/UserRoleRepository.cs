using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserRoleRepository : IUserRoleRepository
{
    private readonly ISqlSugarClient _db;

    public UserRoleRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserRole>> QueryByUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<UserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByRoleIdAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<UserRole> userRoles, CancellationToken cancellationToken)
    {
        if (userRoles.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(userRoles.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<long>> QueryRoleIdsByUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<long>();
        }

        var list = await _db.Queryable<UserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && userIds.Contains(x.UserId))
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<long>> QueryUserIdsByRoleIdAsync(
        TenantId tenantId,
        long roleId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<UserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        return list;
    }
}
