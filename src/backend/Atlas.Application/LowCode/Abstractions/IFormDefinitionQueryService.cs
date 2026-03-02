using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IFormDefinitionQueryService
{
    Task<PagedResult<FormDefinitionListItem>> QueryAsync(
        PagedRequest request, TenantId tenantId, string? category = null,
        CancellationToken cancellationToken = default);

    Task<FormDefinitionDetail?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default);
}
