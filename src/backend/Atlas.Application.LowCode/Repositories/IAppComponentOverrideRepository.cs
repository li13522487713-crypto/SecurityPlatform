using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface IAppComponentOverrideRepository
{
    Task<IReadOnlyList<AppComponentOverride>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<AppComponentOverride?> FindAsync(TenantId tenantId, string type, CancellationToken cancellationToken);
    Task<long> InsertAsync(AppComponentOverride entity, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(AppComponentOverride entity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(TenantId tenantId, string type, CancellationToken cancellationToken);
}
