using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 部门负责人映射仓储实现
/// </summary>
public sealed class ApprovalDepartmentLeaderRepository : IApprovalDepartmentLeaderRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalDepartmentLeaderRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalDepartmentLeader entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalDepartmentLeader entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalDepartmentLeader?> GetByDepartmentIdAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalDepartmentLeader>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DepartmentId == departmentId)
            .FirstAsync(cancellationToken);
    }

    public async Task<long?> GetLeaderUserIdAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken)
    {
        var entity = await GetByDepartmentIdAsync(tenantId, departmentId, cancellationToken);
        return entity?.LeaderUserId;
    }

    public async Task DeleteByDepartmentIdAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken)
    {
        await _db.Deleteable<ApprovalDepartmentLeader>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DepartmentId == departmentId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
