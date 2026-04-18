using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IAppTemplateService
{
    Task<long> UpsertAsync(TenantId tenantId, long currentUserId, AppTemplateUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AppTemplateDto>> SearchAsync(TenantId tenantId, string? keyword, string? kind, string? shareScope, string? industryTag, int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<AppTemplateDto?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<AppTemplateApplyResult> ApplyAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
    Task<int> StarAsync(TenantId tenantId, long currentUserId, long id, bool increment, CancellationToken cancellationToken);
}
