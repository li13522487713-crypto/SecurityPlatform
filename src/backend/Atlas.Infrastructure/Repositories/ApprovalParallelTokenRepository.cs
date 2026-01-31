using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 并行网关 Token 仓储实现
/// </summary>
public sealed class ApprovalParallelTokenRepository : IApprovalParallelTokenRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalParallelTokenRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalParallelToken entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ApprovalParallelToken> entities, CancellationToken cancellationToken)
    {
        var list = entities.ToList();
        if (list.Count == 0)
        {
            return;
        }

        await _db.Insertable(list).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalParallelToken entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<ApprovalParallelToken> entities, CancellationToken cancellationToken)
    {
        var list = entities.ToList();
        if (list.Count == 0)
        {
            return;
        }

        await _db.Updateable(list).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalParallelToken>> GetByInstanceAndGatewayAsync(
        TenantId tenantId,
        long instanceId,
        string gatewayNodeId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalParallelToken>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.GatewayNodeId == gatewayNodeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalParallelToken>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalParallelToken>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
