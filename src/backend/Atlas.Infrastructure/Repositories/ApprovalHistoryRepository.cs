using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批历史事件仓储实现
/// </summary>
public sealed class ApprovalHistoryRepository : IApprovalHistoryRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalHistoryRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalHistoryEvent entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ApprovalHistoryEvent> entities, CancellationToken cancellationToken)
    {
        var list = entities as List<ApprovalHistoryEvent> ?? entities.ToList();
        if (list.Count == 0) return;
        await _db.Insertable(list).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ApprovalHistoryEvent> Items, int TotalCount)> GetPagedByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ApprovalHistoryEvent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }
}
