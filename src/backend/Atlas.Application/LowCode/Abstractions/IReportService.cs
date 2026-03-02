using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IReportService
{
    Task<PagedResult<ReportDefinitionListItem>> QueryAsync(PagedRequest request, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<ReportDefinitionDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<long> CreateAsync(TenantId tenantId, long userId, ReportDefinitionCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long userId, long id, ReportDefinitionUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
}
