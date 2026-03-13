using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiAppPublishRecord : TenantEntity
{
    public AiAppPublishRecord()
        : base(TenantId.Empty)
    {
        Version = string.Empty;
        ReleaseNote = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiAppPublishRecord(
        TenantId tenantId,
        long appId,
        string version,
        string? releaseNote,
        long publishedByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        AppId = appId;
        Version = version;
        ReleaseNote = releaseNote ?? string.Empty;
        PublishedByUserId = publishedByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public long AppId { get; private set; }
    public string Version { get; private set; }
    public string? ReleaseNote { get; private set; }
    public long PublishedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
