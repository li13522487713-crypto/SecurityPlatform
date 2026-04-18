using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface IAppFaqRepository
{
    Task<long> InsertAsync(AppFaqEntry entry, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(AppFaqEntry entry, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<AppFaqEntry?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AppFaqEntry>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken);
}
