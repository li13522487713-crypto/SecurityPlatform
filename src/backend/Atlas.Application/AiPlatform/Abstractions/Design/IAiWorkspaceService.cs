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

    Task<AiLibraryMutationResult> ImportLibraryItemAsync(
        TenantId tenantId,
        long userId,
        AiLibraryImportRequest request,
        CancellationToken cancellationToken);

    Task<AiLibraryMutationResult> ExportLibraryItemAsync(
        TenantId tenantId,
        long userId,
        AiLibraryExportRequest request,
        CancellationToken cancellationToken);

    Task<AiLibraryMutationResult> MoveLibraryItemAsync(
        TenantId tenantId,
        long userId,
        AiLibraryMoveRequest request,
        CancellationToken cancellationToken);
}
