using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批站内信仓储实现
/// </summary>
public sealed class ApprovalInboxMessageRepository : IApprovalInboxMessageRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalInboxMessageRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalInboxMessage entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ApprovalInboxMessage> entities, CancellationToken cancellationToken)
    {
        await _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalInboxMessage entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalInboxMessage?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalInboxMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ApprovalInboxMessage> Items, int TotalCount)> GetPagedByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        int pageIndex,
        int pageSize,
        bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<ApprovalInboxMessage>()
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

    public async Task<int> GetUnreadCountAsync(
        TenantId tenantId,
        long recipientUserId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalInboxMessage>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.RecipientUserId == recipientUserId
                && !x.IsRead)
            .CountAsync(cancellationToken);
    }
}
