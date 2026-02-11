using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Abstractions;

public interface IFormDefinitionRepository
{
    Task<FormDefinition?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormDefinition>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FormDefinition> Items, int Total)> GetPagedAsync(
        TenantId tenantId, int pageIndex, int pageSize, string? keyword, string? category,
        CancellationToken cancellationToken = default);
    Task InsertAsync(FormDefinition entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(FormDefinition entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(TenantId tenantId, string name, long? excludeId = null, CancellationToken cancellationToken = default);
}
