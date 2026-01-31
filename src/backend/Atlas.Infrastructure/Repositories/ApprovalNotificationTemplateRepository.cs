using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 审批通知模板仓储实现
/// </summary>
public sealed class ApprovalNotificationTemplateRepository : IApprovalNotificationTemplateRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalNotificationTemplateRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalNotificationTemplate entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalNotificationTemplate entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalNotificationTemplate?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalNotificationTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<ApprovalNotificationTemplate?> GetByFlowAndEventAsync(
        TenantId tenantId,
        long flowDefinitionId,
        ApprovalNotificationEventType eventType,
        ApprovalNotificationChannel channel,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalNotificationTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.FlowDefinitionId == flowDefinitionId
                && x.EventType == eventType
                && x.Channel == channel
                && x.IsEnabled)
            .FirstAsync(cancellationToken);
    }

    public async Task<ApprovalNotificationTemplate?> GetSystemTemplateAsync(
        TenantId tenantId,
        ApprovalNotificationEventType eventType,
        ApprovalNotificationChannel channel,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalNotificationTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.FlowDefinitionId == 0
                && x.EventType == eventType
                && x.Channel == channel
                && x.IsEnabled)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalNotificationTemplate>> GetByFlowAsync(
        TenantId tenantId,
        long flowDefinitionId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalNotificationTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.FlowDefinitionId == flowDefinitionId)
            .OrderBy(x => x.EventType)
            .OrderBy(x => x.Channel)
            .ToListAsync(cancellationToken);
    }
}
