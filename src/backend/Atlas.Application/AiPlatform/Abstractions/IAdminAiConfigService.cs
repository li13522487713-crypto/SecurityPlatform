using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAdminAiConfigService
{
    Task<AdminAiConfigDto> GetAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        AdminAiConfigUpdateRequest request,
        CancellationToken cancellationToken);
}
