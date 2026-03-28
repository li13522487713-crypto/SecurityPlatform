using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserDepartmentRepository : IUserDepartmentRepository
{
    private readonly ISqlSugarClient _db;

    public UserDepartmentRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserDepartment>> QueryByUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<UserDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<UserDepartment>> QueryByUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<UserDepartment>();
        }

        var ids = userIds.Distinct().ToArray();
        var list = await _db.Queryable<UserDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.UserId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByUserAndDepartmentIdsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken)
    {
        if (departmentIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var ids = departmentIds.Distinct().ToArray();
        return _db.Deleteable<UserDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && SqlFunc.ContainsArray(ids, x.DepartmentId))
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByDepartmentIdAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<UserDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DepartmentId == departmentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<UserDepartment> userDepartments, CancellationToken cancellationToken)
    {
        if (userDepartments.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(userDepartments.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
