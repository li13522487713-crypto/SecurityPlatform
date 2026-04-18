using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiDatabaseRecord : TenantEntity
{
    public AiDatabaseRecord()
        : base(TenantId.Empty)
    {
        DataJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public AiDatabaseRecord(
        TenantId tenantId,
        long databaseId,
        string dataJson,
        long id)
        : this(tenantId, databaseId, dataJson, id, ownerUserId: null, creatorUserId: null, channelId: null)
    {
    }

    /// <summary>
    /// D1：行级元数据。owner/creator 用于 SingleUser/MultiUser 模式过滤；
    /// channelId 用于 ChannelScope 过滤（D2 配合）。旧记录 owner/creator/channel 均为 NULL，
    /// 默认按 MultiUser 兼容方式处理（NULL 视为对所有用户可见）。
    /// </summary>
    public AiDatabaseRecord(
        TenantId tenantId,
        long databaseId,
        string dataJson,
        long id,
        long? ownerUserId,
        long? creatorUserId,
        string? channelId)
        : base(tenantId)
    {
        Id = id;
        DatabaseId = databaseId;
        DataJson = string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson;
        OwnerUserId = ownerUserId;
        CreatorUserId = creatorUserId;
        ChannelId = string.IsNullOrWhiteSpace(channelId) ? null : channelId.Trim();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatabaseId { get; private set; }
    public string DataJson { get; private set; }
    public long? OwnerUserId { get; private set; }
    public long? CreatorUserId { get; private set; }
    public string? ChannelId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void UpdateData(string dataJson)
    {
        DataJson = string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reassign(long? ownerUserId, string? channelId)
    {
        OwnerUserId = ownerUserId;
        ChannelId = string.IsNullOrWhiteSpace(channelId) ? null : channelId.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
