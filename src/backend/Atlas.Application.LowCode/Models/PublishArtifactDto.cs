namespace Atlas.Application.LowCode.Models;

public sealed record PublishArtifactDto(
    string Id,
    string AppId,
    string VersionId,
    string Kind,
    string Status,
    string Fingerprint,
    string? PublicUrl,
    string RendererMatrixJson,
    string? ErrorMessage,
    string PublishedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PublishRequest(
    string Kind, // hosted / embedded-sdk / preview
    string? VersionId,
    string? RendererMatrixJson);

public sealed record PublishRollbackRequest(string ArtifactId);
