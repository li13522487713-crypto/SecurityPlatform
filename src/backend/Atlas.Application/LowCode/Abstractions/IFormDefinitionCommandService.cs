using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IFormDefinitionCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long userId, FormDefinitionCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long userId, long id, FormDefinitionUpdateRequest request, CancellationToken cancellationToken = default);
    Task UpdateSchemaAsync(TenantId tenantId, long userId, long id, FormDefinitionSchemaUpdateRequest request, CancellationToken cancellationToken = default);
    Task PublishAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task DisableAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task EnableAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task RollbackToVersionAsync(TenantId tenantId, long userId, long id, long versionId, CancellationToken cancellationToken = default);
    Task DeprecateAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
}
