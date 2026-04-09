namespace Atlas.Application.Platform.Models;

public sealed record ReleaseBundleResponse(
    string BundleId,
    string ReleaseId,
    string ManifestId,
    string BundleVersion,
    string UnifiedModelJson,
    string RuntimeProjectionJson,
    string RuntimeManifestSetJson,
    string OrchestrationPlanSetJson,
    string ToolReleaseRefsJson,
    string KnowledgeSnapshotRefsJson,
    string ResourceBindingSnapshotJson,
    string NavigationProjectionSnapshotJson,
    string ExposureCatalogSnapshotJson,
    string SignatureJson,
    string CreatedBy,
    string CreatedAt);
