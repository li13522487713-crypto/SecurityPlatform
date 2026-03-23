using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AgentPublicationListItem(
    long Id,
    long AgentId,
    int Version,
    bool IsActive,
    string EmbedToken,
    DateTime EmbedTokenExpiresAt,
    string? ReleaseNote,
    long PublishedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? RevokedAt);

public sealed record AgentPublicationPublishRequest(string? ReleaseNote);

public sealed record AgentPublicationRollbackRequest(int TargetVersion, string? ReleaseNote);

public sealed record AgentPublicationPublishResult(
    long PublicationId,
    long AgentId,
    int Version,
    string EmbedToken,
    DateTime EmbedTokenExpiresAt);

public sealed record AgentEmbedTokenResult(
    long PublicationId,
    long AgentId,
    int Version,
    string EmbedToken,
    DateTime EmbedTokenExpiresAt);

public sealed record AgentPublicationTokenContext(
    TenantId TenantId,
    long AgentId,
    long PublicationId,
    int Version);
