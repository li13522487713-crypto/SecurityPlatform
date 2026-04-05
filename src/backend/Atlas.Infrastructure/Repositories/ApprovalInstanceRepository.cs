using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批流程实例仓储实现
/// </summary>
public sealed class ApprovalInstanceRepository : IApprovalInstanceRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalInstanceRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalProcessInstance entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalProcessInstance entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalProcessInstance?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalProcessInstance>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ApprovalProcessInstance>();
        }

        var distinctIds = ids.Distinct().ToArray();
        return await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(distinctIds, x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApprovalProcessInstance?> GetByBusinessKeyAsync(
        TenantId tenantId,
        string businessKey,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.BusinessKey == businessKey)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ApprovalProcessInstance> Items, int TotalCount)> GetPagedByInitiatorAsync(
        TenantId tenantId,
        long initiatorUserId,
        int pageIndex,
        int pageSize,
        ApprovalInstanceStatus? status = null,
        IReadOnlyList<long>? restrictInitiatorUserIds = null,
        CancellationToken cancellationToken = default)
    {
        if (restrictInitiatorUserIds is not null && restrictInitiatorUserIds.Count == 0)
        {
            return (Array.Empty<ApprovalProcessInstance>(), 0);
        }

        var query = _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InitiatorUserId == initiatorUserId);

        if (restrictInitiatorUserIds is not null)
        {
            var uidArray = restrictInitiatorUserIds.Distinct().ToArray();
            query = query.Where(x => SqlFunc.ContainsArray(uidArray, x.InitiatorUserId));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.StartedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<ApprovalProcessInstance> Items, int TotalCount)> GetPagedAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        long? definitionId = null,
        long? initiatorUserId = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        string? businessKey = null,
        ApprovalInstanceStatus? status = null,
        IReadOnlyList<long>? restrictInitiatorUserIds = null,
        CancellationToken cancellationToken = default)
    {
        if (restrictInitiatorUserIds is not null && restrictInitiatorUserIds.Count == 0)
        {
            return (Array.Empty<ApprovalProcessInstance>(), 0);
        }

        var query = _db.Queryable<ApprovalProcessInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (definitionId.HasValue)
        {
            query = query.Where(x => x.DefinitionId == definitionId.Value);
        }

        if (initiatorUserId.HasValue)
        {
            query = query.Where(x => x.InitiatorUserId == initiatorUserId.Value);
        }

        if (restrictInitiatorUserIds is not null)
        {
            var uidArray = restrictInitiatorUserIds.Distinct().ToArray();
            query = query.Where(x => SqlFunc.ContainsArray(uidArray, x.InitiatorUserId));
        }

        if (startedFrom.HasValue)
        {
            query = query.Where(x => x.StartedAt >= startedFrom.Value);
        }

        if (startedTo.HasValue)
        {
            query = query.Where(x => x.StartedAt <= startedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(businessKey))
        {
            query = query.Where(x => x.BusinessKey.Contains(businessKey));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.StartedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }
}
