using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

[SugarTable("AiDatabaseChannelConfig")]
public sealed class AiDatabaseChannelConfig : TenantEntity
{
    public AiDatabaseChannelConfig()
        : base(TenantId.Empty)
    {
        ChannelKey = string.Empty;
        DisplayName = string.Empty;
        PublishChannelType = string.Empty;
        CredentialKind = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public AiDatabaseChannelConfig(
        TenantId tenantId,
        long databaseId,
        string channelKey,
        string displayName,
        bool allowDraft,
        bool allowOnline,
        string? publishChannelType,
        string? credentialKind,
        int sortOrder,
        long id)
        : base(tenantId)
    {
        Id = id;
        DatabaseId = databaseId;
        ChannelKey = channelKey.Trim();
        DisplayName = displayName.Trim();
        AllowDraft = allowDraft;
        AllowOnline = allowOnline;
        PublishChannelType = publishChannelType?.Trim() ?? string.Empty;
        CredentialKind = credentialKind?.Trim() ?? string.Empty;
        SortOrder = sortOrder;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatabaseId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string ChannelKey { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string DisplayName { get; private set; }

    public bool AllowDraft { get; private set; }

    public bool AllowOnline { get; private set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string PublishChannelType { get; private set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string CredentialKind { get; private set; }

    public int SortOrder { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Update(bool allowDraft, bool allowOnline, int sortOrder)
    {
        AllowDraft = allowDraft;
        AllowOnline = allowOnline;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
