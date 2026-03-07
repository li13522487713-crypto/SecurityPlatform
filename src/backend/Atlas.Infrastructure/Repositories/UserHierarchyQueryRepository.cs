using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserHierarchyQueryRepository : IUserHierarchyQueryRepository
{
    private const int MaxRecursionCap = 100;

    private readonly ISqlSugarClient _db;

    public UserHierarchyQueryRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<long>> GetLeaderChainAsync(
        TenantId tenantId,
        long userId,
        int maxLevels,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxLevels <= 0)
        {
            return Array.Empty<long>();
        }

        var depth = Math.Min(maxLevels, MaxRecursionCap);
        return await BuildLeaderChainAsync(tenantId, userId, depth, cancellationToken);
    }

    public async Task<long?> GetLeaderAtLevelAsync(
        TenantId tenantId,
        long userId,
        int level,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (level < 1)
        {
            return null;
        }

        var depth = Math.Min(level, MaxRecursionCap);
        var rows = await BuildLeaderChainAsync(tenantId, userId, depth, cancellationToken);
        return rows.Count >= level ? rows[level - 1] : null;
    }

    private async Task<IReadOnlyList<long>> BuildLeaderChainAsync(
        TenantId tenantId,
        long userId,
        int maxLevels,
        CancellationToken cancellationToken)
    {
        var tenantGuid = tenantId.Value;
        var userDepartments = await _db.Queryable<UserDepartment>()
            .Where(x => x.TenantIdValue == tenantGuid)
            .ToListAsync(cancellationToken);
        var departmentLeaders = await _db.Queryable<ApprovalDepartmentLeader>()
            .Where(x => x.TenantIdValue == tenantGuid)
            .ToListAsync(cancellationToken);

        var primaryDepartmentByUser = userDepartments
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var primary = group.FirstOrDefault(x => x.IsPrimary);
                    if (primary is not null)
                    {
                        return primary.DepartmentId;
                    }

                    return group.Min(x => x.DepartmentId);
                });
        var leaderByDepartment = departmentLeaders
            .GroupBy(x => x.DepartmentId)
            .ToDictionary(g => g.Key, g => g.First().LeaderUserId);

        var leaders = new List<long>(maxLevels);
        var visitedUsers = new HashSet<long> { userId };
        var currentUserId = userId;
        var depth = 0;
        while (depth < maxLevels)
        {
            if (!primaryDepartmentByUser.TryGetValue(currentUserId, out var departmentId))
            {
                break;
            }

            if (!leaderByDepartment.TryGetValue(departmentId, out var leaderUserId))
            {
                break;
            }

            if (!visitedUsers.Add(leaderUserId))
            {
                break;
            }

            leaders.Add(leaderUserId);
            currentUserId = leaderUserId;
            depth++;
        }

        return leaders;
    }
}
