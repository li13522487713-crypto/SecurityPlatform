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
        RuntimeManifestSetJson = "{}";
        OrchestrationPlanSetJson = "{}";
        ToolReleaseRefsJson = "[]";
        KnowledgeSnapshotRefsJson = "[]";
        ResourceBindingSnapshotJson = "{}";
        NavigationProjectionSnapshotJson = "{}";
        ExposureCatalogSnapshotJson = "{}";
        SignatureJson = "{}";
    }

    public ReleaseBundle(
        TenantId tenantId,
        long id,
        long releaseId,
        long manifestId,
        string bundleVersion,
        string unifiedModelJson,
        string runtimeProjectionJson,
        string runtimeManifestSetJson,
        string orchestrationPlanSetJson,
        string toolReleaseRefsJson,
        string knowledgeSnapshotRefsJson,
        string resourceBindingSnapshotJson,
        string navigationProjectionSnapshotJson,
        string exposureCatalogSnapshotJson,
        string signatureJson,
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
        RuntimeManifestSetJson = runtimeManifestSetJson;
        OrchestrationPlanSetJson = orchestrationPlanSetJson;
        ToolReleaseRefsJson = toolReleaseRefsJson;
        KnowledgeSnapshotRefsJson = knowledgeSnapshotRefsJson;
        ResourceBindingSnapshotJson = resourceBindingSnapshotJson;
        NavigationProjectionSnapshotJson = navigationProjectionSnapshotJson;
        ExposureCatalogSnapshotJson = exposureCatalogSnapshotJson;
        SignatureJson = signatureJson;
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
    public string RuntimeManifestSetJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string OrchestrationPlanSetJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ToolReleaseRefsJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string KnowledgeSnapshotRefsJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ResourceBindingSnapshotJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string NavigationProjectionSnapshotJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ExposureCatalogSnapshotJson { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string SignatureJson { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
