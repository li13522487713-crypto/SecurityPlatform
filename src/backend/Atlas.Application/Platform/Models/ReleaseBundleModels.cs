namespace Atlas.Application.Platform.Models;

public sealed record ReleaseBundleResponse(
    string BundleId,
    string ReleaseId,
    string ManifestId,
    string BundleVersion,
    string UnifiedModelJson,
    string RuntimeProjectionJson,
    string NavigationProjectionSnapshotJson,
    string CreatedBy,
    string CreatedAt);
