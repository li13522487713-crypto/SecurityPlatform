using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicViews.Abstractions;

public interface IDynamicViewQueryService
{
    Task<PagedResult<DynamicViewListItem>> QueryAsync(
        TenantId tenantId,
        long? appId,
        PagedRequest request,
        CancellationToken cancellationToken);

    Task<DynamicViewDefinitionDto?> GetByKeyAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicViewHistoryItemDto>> GetHistoryAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task<DeleteCheckResultDto> GetDeleteCheckAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task<DynamicRecordListResult> QueryRecordsAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        DynamicViewRecordsQueryRequest request,
        CancellationToken cancellationToken);

    Task<DynamicRecordListResult> PreviewAsync(
        TenantId tenantId,
        long? appId,
        DynamicViewPreviewRequest request,
        CancellationToken cancellationToken);

    Task<DynamicViewSqlPreviewDto> PreviewSqlAsync(
        TenantId tenantId,
        long? appId,
        DynamicViewSqlPreviewRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeleteCheckBlockerDto>> GetReferencesAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);
}
