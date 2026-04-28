using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;

namespace Atlas.Application.ExternalConnectors.Repositories;

public interface IExternalApprovalTemplateCacheRepository
{
    Task UpsertAsync(ExternalApprovalTemplateCache entity, CancellationToken cancellationToken);

    Task<ExternalApprovalTemplateCache?> GetAsync(TenantId tenantId, long providerId, string externalTemplateId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalApprovalTemplateCache>> ListByProviderAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken);
}

public interface IExternalApprovalTemplateMappingRepository
{
    Task AddAsync(ExternalApprovalTemplateMapping entity, CancellationToken cancellationToken);

    Task UpdateAsync(ExternalApprovalTemplateMapping entity, CancellationToken cancellationToken);

    Task<ExternalApprovalTemplateMapping?> GetByFlowAsync(TenantId tenantId, long providerId, long flowDefinitionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalApprovalTemplateMapping>> ListByProviderAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}

public interface IExternalApprovalInstanceLinkRepository
{
    Task AddAsync(ExternalApprovalInstanceLink entity, CancellationToken cancellationToken);

    Task UpdateAsync(ExternalApprovalInstanceLink entity, CancellationToken cancellationToken);

    Task<ExternalApprovalInstanceLink?> GetByLocalAsync(TenantId tenantId, long providerId, long localInstanceId, CancellationToken cancellationToken);

    Task<ExternalApprovalInstanceLink?> GetByExternalAsync(TenantId tenantId, long providerId, string externalInstanceId, CancellationToken cancellationToken);
}
