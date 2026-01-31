using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// 外部回调配置仓储实现
/// </summary>
public sealed class ApprovalExternalCallbackConfigRepository : IApprovalExternalCallbackConfigRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalExternalCallbackConfigRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ApprovalExternalCallbackConfig entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalExternalCallbackConfig entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalExternalCallbackConfig?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalExternalCallbackConfig>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ApprovalExternalCallbackConfig>();
        }

        var distinctIds = ids.Distinct().ToArray();
        return await _db.Queryable<ApprovalExternalCallbackConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && distinctIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalExternalCallbackConfig>> GetByFlowAndEventAsync(
        TenantId tenantId,
        long flowDefinitionId,
        CallbackEventType eventType,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.FlowDefinitionId == flowDefinitionId
                && x.EventType == eventType
                && x.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalExternalCallbackConfig>> GetSystemConfigsAsync(
        TenantId tenantId,
        CallbackEventType eventType,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.FlowDefinitionId == 0
                && x.EventType == eventType
                && x.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalExternalCallbackConfig>> GetByFlowAsync(
        TenantId tenantId,
        long flowDefinitionId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalExternalCallbackConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.FlowDefinitionId == flowDefinitionId)
            .OrderBy(x => x.EventType)
            .ToListAsync(cancellationToken);
    }
}
