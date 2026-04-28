using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.ExternalConnectors.Repositories;

public sealed class ExternalApprovalTemplateCacheRepository : IExternalApprovalTemplateCacheRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalApprovalTemplateCacheRepository(ISqlSugarClient db) { _db = db; }

    public async Task UpsertAsync(ExternalApprovalTemplateCache entity, CancellationToken cancellationToken)
    {
        var existing = await _db.Queryable<ExternalApprovalTemplateCache>()
            .Where(x => x.TenantIdValue == entity.TenantIdValue && x.ProviderId == entity.ProviderId && x.ExternalTemplateId == entity.ExternalTemplateId)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await _db.Updateable(entity)
                .Where(x => x.Id == existing.Id && x.TenantIdValue == entity.TenantIdValue)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<ExternalApprovalTemplateCache?> GetAsync(TenantId tenantId, long providerId, string externalTemplateId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalApprovalTemplateCache>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.ExternalTemplateId == externalTemplateId)
            .FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalApprovalTemplateCache>> ListByProviderAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalApprovalTemplateCache>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId)
            .OrderBy(x => x.FetchedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
}

public sealed class ExternalApprovalTemplateMappingRepository : IExternalApprovalTemplateMappingRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalApprovalTemplateMappingRepository(ISqlSugarClient db) { _db = db; }

    public async Task AddAsync(ExternalApprovalTemplateMapping entity, CancellationToken cancellationToken)
        => await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public async Task UpdateAsync(ExternalApprovalTemplateMapping entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<ExternalApprovalTemplateMapping?> GetByFlowAsync(TenantId tenantId, long providerId, long flowDefinitionId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalApprovalTemplateMapping>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.FlowDefinitionId == flowDefinitionId)
            .FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalApprovalTemplateMapping>> ListByProviderAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalApprovalTemplateMapping>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId)
            .ToListAsync(cancellationToken);

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => await _db.Deleteable<ExternalApprovalTemplateMapping>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
}

public sealed class ExternalApprovalInstanceLinkRepository : IExternalApprovalInstanceLinkRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalApprovalInstanceLinkRepository(ISqlSugarClient db) { _db = db; }

    public async Task AddAsync(ExternalApprovalInstanceLink entity, CancellationToken cancellationToken)
        => await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public async Task UpdateAsync(ExternalApprovalInstanceLink entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<ExternalApprovalInstanceLink?> GetByLocalAsync(TenantId tenantId, long providerId, long localInstanceId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalApprovalInstanceLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.LocalInstanceId == localInstanceId)
            .FirstAsync(cancellationToken);

    public async Task<ExternalApprovalInstanceLink?> GetByExternalAsync(TenantId tenantId, long providerId, string externalInstanceId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalApprovalInstanceLink>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.ExternalInstanceId == externalInstanceId)
            .FirstAsync(cancellationToken);
}
