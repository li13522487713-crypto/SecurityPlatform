using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批流程变量仓储实现
/// </summary>
public sealed class ApprovalProcessVariableRepository : IApprovalProcessVariableRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalProcessVariableRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalProcessVariable entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalProcessVariable entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalProcessVariable?> GetByInstanceAndNameAsync(
        TenantId tenantId,
        long instanceId,
        string variableName,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalProcessVariable>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.VariableName == variableName)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalProcessVariable>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalProcessVariable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        await _db.Deleteable<ApprovalProcessVariable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
