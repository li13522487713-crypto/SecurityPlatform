using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Abstractions;

public interface IFormDefinitionVersionRepository
{
    Task<FormDefinitionVersion?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FormDefinitionVersion>> GetByFormDefinitionIdAsync(
        TenantId tenantId, long formDefinitionId, CancellationToken cancellationToken = default);

    Task InsertAsync(FormDefinitionVersion entity, CancellationToken cancellationToken = default);

    Task DeleteByFormDefinitionIdAsync(
        TenantId tenantId, long formDefinitionId, CancellationToken cancellationToken = default);
}
