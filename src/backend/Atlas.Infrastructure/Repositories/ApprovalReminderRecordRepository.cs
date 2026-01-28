using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 催办记录仓储实现
/// </summary>
public sealed class ApprovalReminderRecordRepository : IApprovalReminderRecordRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalReminderRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalReminderRecord entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalReminderRecord?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalReminderRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalReminderRecord>> GetByInstanceAsync(
        TenantId tenantId,
        long instanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalReminderRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.InstanceId == instanceId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalReminderRecord>> GetByRecipientAsync(
        TenantId tenantId,
        long recipientUserId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalReminderRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RecipientUserId == recipientUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
