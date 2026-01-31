using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批流定义仓储实现
/// </summary>
public sealed class ApprovalFlowRepository : IApprovalFlowRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalFlowRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalFlowDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalFlowDefinition entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalFlowDefinition?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalFlowDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalFlowDefinition>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ApprovalFlowDefinition>();
        }

        var distinctIds = ids.Distinct().ToArray();
        return await _db.Queryable<ApprovalFlowDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(distinctIds, x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ApprovalFlowDefinition> Items, int TotalCount)> GetPagedAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        ApprovalFlowStatus? status = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ApprovalFlowDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        await _db.Deleteable<ApprovalFlowDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }
}
