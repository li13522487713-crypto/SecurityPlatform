using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Platform.Entities;

[SugarIndex("UX_ReleaseBundle_Tenant_Release", nameof(TenantIdValue), OrderByType.Asc, nameof(ReleaseId), OrderByType.Asc, true)]
public sealed class ReleaseBundle : TenantEntity
{
    public ReleaseBundle()
        : base(TenantId.Empty)
    {
        BundleVersion = string.Empty;
        UnifiedModelJson = "{}";
        RuntimeProjectionJson = "{}";
        NavigationProjectionSnapshotJson = "{}";
    }

    public ReleaseBundle(
        TenantId tenantId,
        long id,
        long releaseId,
        long manifestId,
        string bundleVersion,
        string unifiedModelJson,
        string runtimeProjectionJson,
        string navigationProjectionSnapshotJson,
        long createdBy,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        ReleaseId = releaseId;
        ManifestId = manifestId;
        BundleVersion = bundleVersion;
        UnifiedModelJson = unifiedModelJson;
        RuntimeProjectionJson = runtimeProjectionJson;
        NavigationProjectionSnapshotJson = navigationProjectionSnapshotJson;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public long ReleaseId { get; private set; }
    public long ManifestId { get; private set; }
    public string BundleVersion { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string UnifiedModelJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string RuntimeProjectionJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string NavigationProjectionSnapshotJson { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
