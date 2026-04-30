using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMendixDomainModelService
{
    Task<MendixDomainModelDocumentDto> GetOrCreateAsync(string appId, string workspaceId, string moduleId, CancellationToken cancellationToken);

    Task<MendixDomainModelDocumentDto> SaveAsync(
        string appId,
        string workspaceId,
        string moduleId,
        MendixDomainModelDocumentDto document,
        long? updatedByUserId,
        CancellationToken cancellationToken);

    Task<MendixDomainModelDocumentDto> UpdateBindingsAsync(
        string appId,
        string workspaceId,
        string moduleId,
        IReadOnlyList<MendixDomainModelBindingDto> bindings,
        long? updatedByUserId,
        CancellationToken cancellationToken);

    Task<MendixDomainModelImportResultDto> ImportTablesAsync(
        string appId,
        string workspaceId,
        string moduleId,
        MendixDomainModelImportTablesRequestDto request,
        long? updatedByUserId,
        CancellationToken cancellationToken);

    Task<MendixDomainModelSyncPlanDto> PreviewSyncAsync(
        string appId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken);

    Task<MendixDomainModelSyncResultDto> SyncDraftAsync(
        string appId,
        string workspaceId,
        string moduleId,
        long? updatedByUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MendixDomainModelModuleSummaryDto>> ListModuleSummariesAsync(
        string appId,
        string workspaceId,
        CancellationToken cancellationToken);

    Task<MendixDomainModelMetadataCatalogDto?> GetMetadataCatalogAsync(
        string? appId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken);
}
