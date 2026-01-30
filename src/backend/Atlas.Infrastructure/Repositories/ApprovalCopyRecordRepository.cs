using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 抄送记录仓储实现
/// </summary>
public sealed class ApprovalCopyRecordRepository : IApprovalCopyRecordRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalCopyRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalCopyRecord entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ApprovalCopyRecord> entities, CancellationToken cancellationToken)
    {
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalCopyRecord entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalCopyRecord?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalCopyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalCopyRecord>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalCopyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalCopyRecord>> GetByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalCopyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RecipientUserId == recipientUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalCopyRecord>> GetByInstanceAndNodeAsync(
        TenantId tenantId,
        long instanceId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalCopyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.NodeId == nodeId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByInstanceAndRecipientAsync(
        TenantId tenantId,
        long instanceId,
        long recipientUserId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalCopyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.InstanceId == instanceId
                && x.RecipientUserId == recipientUserId)
            .AnyAsync();
    }

    public async Task<(IReadOnlyList<ApprovalCopyRecord> Items, int TotalCount)> GetPagedByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        int pageIndex,
        int pageSize,
        bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ApprovalCopyRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RecipientUserId == recipientUserId);

        if (isRead.HasValue)
        {
            query = query.Where(x => x.IsRead == isRead.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }
}
