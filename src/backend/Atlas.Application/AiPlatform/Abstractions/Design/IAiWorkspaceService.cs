using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiWorkspaceService
{
    Task<AiWorkspaceDto> GetCurrentAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    Task<AiWorkspaceDto> UpdateAsync(
        TenantId tenantId,
        long userId,
        AiWorkspaceUpdateRequest request,
        CancellationToken cancellationToken);

    Task<AiLibraryPagedResult> GetLibraryAsync(
        TenantId tenantId,
        AiLibraryQueryRequest request,
        CancellationToken cancellationToken);
}
