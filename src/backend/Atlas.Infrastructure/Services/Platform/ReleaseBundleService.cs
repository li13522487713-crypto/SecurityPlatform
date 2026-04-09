using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class ReleaseBundleQueryService : IReleaseBundleQueryService
{
    private readonly ISqlSugarClient _db;

    public ReleaseBundleQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<ReleaseBundleResponse?> GetByReleaseIdAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default)
    {
        var bundle = await _db.Queryable<ReleaseBundle>()
            .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.ReleaseId == releaseId, cancellationToken);
        return bundle is null ? null : Map(bundle);
    }

    public async Task<ReleaseBundleResponse?> GetActiveByManifestIdAsync(
        TenantId tenantId,
        long manifestId,
        CancellationToken cancellationToken = default)
    {
        var release = await _db.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantId.Value
                && item.ManifestId == manifestId
                && item.Status == AppReleaseStatus.Released)
            .OrderByDescending(item => item.ReleasedAt)
            .FirstAsync(cancellationToken);
        if (release is null)
        {
            return null;
        }

        return await GetByReleaseIdAsync(tenantId, release.Id, cancellationToken);
    }

    private static ReleaseBundleResponse Map(ReleaseBundle bundle)
    {
        return new ReleaseBundleResponse(
            bundle.Id.ToString(),
            bundle.ReleaseId.ToString(),
            bundle.ManifestId.ToString(),
            bundle.BundleVersion,
            bundle.UnifiedModelJson,
            bundle.RuntimeProjectionJson,
            bundle.RuntimeManifestSetJson,
            bundle.OrchestrationPlanSetJson,
            bundle.ToolReleaseRefsJson,
            bundle.KnowledgeSnapshotRefsJson,
            bundle.ResourceBindingSnapshotJson,
            bundle.NavigationProjectionSnapshotJson,
            bundle.ExposureCatalogSnapshotJson,
            bundle.SignatureJson,
            bundle.CreatedBy.ToString(),
            bundle.CreatedAt.ToString("O"));
    }
}
