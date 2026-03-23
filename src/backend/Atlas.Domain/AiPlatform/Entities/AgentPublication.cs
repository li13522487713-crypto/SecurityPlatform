using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AgentPublication : TenantEntity
{
    public AgentPublication()
        : base(TenantId.Empty)
    {
        SnapshotJson = "{}";
        EmbedToken = string.Empty;
        ReleaseNote = string.Empty;
        CreatedAt = DateTime.UtcNow;
        EmbedTokenExpiresAt = CreatedAt;
        IsActive = true;
        RevokedAt = DateTime.UnixEpoch;
    }

    public AgentPublication(
        TenantId tenantId,
        long agentId,
        int version,
        string snapshotJson,
        string embedToken,
        DateTime embedTokenExpiresAt,
        string? releaseNote,
        long publishedByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        AgentId = agentId;
        Version = version;
        SnapshotJson = snapshotJson;
        EmbedToken = embedToken;
        EmbedTokenExpiresAt = embedTokenExpiresAt;
        ReleaseNote = releaseNote ?? string.Empty;
        PublishedByUserId = publishedByUserId;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        RevokedAt = DateTime.UnixEpoch;
    }

    public long AgentId { get; private set; }
    public int Version { get; private set; }
    public string SnapshotJson { get; private set; }
    public string EmbedToken { get; private set; }
    public DateTime EmbedTokenExpiresAt { get; private set; }
    public bool IsActive { get; private set; }
    public string? ReleaseNote { get; private set; }
    public long PublishedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RevokedAt = DateTime.UtcNow;
        UpdatedAt = RevokedAt;
    }

    public void RotateEmbedToken(string embedToken, DateTime embedTokenExpiresAt)
    {
        EmbedToken = embedToken;
        EmbedTokenExpiresAt = embedTokenExpiresAt;
        UpdatedAt = DateTime.UtcNow;
    }
}
