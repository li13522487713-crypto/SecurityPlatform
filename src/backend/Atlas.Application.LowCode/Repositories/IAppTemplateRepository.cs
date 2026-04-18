using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface IAppTemplateRepository
{
    Task<long> InsertAsync(AppTemplate entity, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(AppTemplate entity, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<AppTemplate?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<bool> ExistsCodeAsync(TenantId tenantId, string code, long? excludeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AppTemplate>> SearchAsync(TenantId tenantId, string? keyword, string? kind, string? shareScope, string? industryTag, int pageIndex, int pageSize, CancellationToken cancellationToken);
}
