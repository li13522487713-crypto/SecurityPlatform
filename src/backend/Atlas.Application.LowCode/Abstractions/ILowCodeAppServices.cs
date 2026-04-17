using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>应用定义查询服务（M01）。</summary>
public interface IAppDefinitionQueryService
{
    Task<PagedResult<AppDefinitionListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        string? status,
        CancellationToken cancellationToken);

    Task<AppDefinitionDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<AppDraftResponse?> GetDraftAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<AppSchemaSnapshotDto?> GetSchemaSnapshotAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppVersionArchiveListItem>> ListVersionsAsync(
        TenantId tenantId,
        long id,
        bool includeSystemSnapshot,
        CancellationToken cancellationToken);
}

/// <summary>应用定义命令服务（M01）。</summary>
public interface IAppDefinitionCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long currentUserId,
        AppDefinitionCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateMetadataAsync(
        TenantId tenantId,
        long currentUserId,
        long id,
        AppDefinitionUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);

    Task ReplaceDraftAsync(
        TenantId tenantId,
        long currentUserId,
        long id,
        AppDraftReplaceRequest request,
        CancellationToken cancellationToken);

    Task AutoSaveDraftAsync(
        TenantId tenantId,
        long currentUserId,
        long id,
        AppDraftAutoSaveRequest request,
        CancellationToken cancellationToken);

    Task<long> CreateVersionSnapshotAsync(
        TenantId tenantId,
        long currentUserId,
        long id,
        AppVersionSnapshotRequest request,
        CancellationToken cancellationToken);
}
