using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ApprovalAgentConfigRepository : IApprovalAgentConfigRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalAgentConfigRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalAgentConfig entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalAgentConfig entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        await _db.Deleteable<ApprovalAgentConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalAgentConfig?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalAgentConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<ApprovalAgentConfig?> GetActiveAgentAsync(
        TenantId tenantId,
        long principalUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalAgentConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.PrincipalUserId == principalUserId
                && x.IsEnabled
                && x.StartTime <= now
                && x.EndTime >= now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, ApprovalAgentConfig>> GetActiveAgentsByUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> principalUserIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (principalUserIds.Count == 0)
        {
            return new Dictionary<long, ApprovalAgentConfig>();
        }

        var ids = principalUserIds.Distinct().ToArray();
        var configs = await _db.Queryable<ApprovalAgentConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && SqlFunc.ContainsArray(ids, x.PrincipalUserId)
                && x.IsEnabled
                && x.StartTime <= now
                && x.EndTime >= now)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // 每个委托人取最新一条生效配置
        return configs
            .GroupBy(c => c.PrincipalUserId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<IReadOnlyList<ApprovalAgentConfig>> GetByPrincipalUserIdAsync(
        TenantId tenantId,
        long principalUserId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalAgentConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PrincipalUserId == principalUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
