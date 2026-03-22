using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodeAppQueryService
{
    Task<PagedResult<LowCodeAppListItem>> QueryAsync(
        PagedRequest request, TenantId tenantId, string? category = null,
        CancellationToken cancellationToken = default);

    Task<LowCodeAppDetail?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default);

    Task<LowCodeAppDetail?> GetByKeyAsync(
        TenantId tenantId, string appKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowCodeAppEntityAliasItem>> GetEntityAliasesAsync(
        TenantId tenantId, long appId, CancellationToken cancellationToken = default);

    Task<LowCodeAppDataSourceInfo?> GetDataSourceInfoAsync(
        TenantId tenantId, long appId, CancellationToken cancellationToken = default);

    Task<PagedResult<LowCodeAppVersionListItem>> GetVersionsAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<LowCodeAppExportPackage?> ExportAsync(
        TenantId tenantId, long appId, CancellationToken cancellationToken = default);

    Task<LowCodePageDetail?> GetPageByIdAsync(
        TenantId tenantId, long pageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowCodePageTreeNode>> GetPageTreeAsync(
        TenantId tenantId, long appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowCodePageVersionListItem>> GetPageVersionsAsync(
        TenantId tenantId, long pageId, CancellationToken cancellationToken = default);

    Task<LowCodePageRuntimeSchema?> GetRuntimePageSchemaAsync(
        TenantId tenantId,
        long pageId,
        string mode,
        string? environmentCode,
        CancellationToken cancellationToken = default);
}
