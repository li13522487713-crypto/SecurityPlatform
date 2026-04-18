using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// D2：基于 <see cref="AiDatabase.QueryMode"/> 与 <see cref="AiDatabase.ChannelScope"/> 的访问策略。
/// 老记录 OwnerUserId/ChannelId 为 NULL 时一律视为全员可见（兼容旧数据）。
/// </summary>
public sealed record AiDatabaseAccessPolicy(long? OwnerUserId, string? ChannelId)
{
    public static AiDatabaseAccessPolicy Open { get; } = new(null, null);

    public static AiDatabaseAccessPolicy For(AiDatabase database, long? userId, string? channelId)
    {
        ArgumentNullException.ThrowIfNull(database);
        var owner = database.QueryMode == AiDatabaseQueryMode.SingleUser ? userId : null;
        var channel = database.ChannelScope == AiDatabaseChannelScope.Channel
            ? (string.IsNullOrWhiteSpace(channelId) ? null : channelId.Trim())
            : null;
        return new AiDatabaseAccessPolicy(owner, channel);
    }

    public bool IsRecordVisible(long? recordOwnerUserId, string? recordChannelId)
    {
        if (OwnerUserId.HasValue && recordOwnerUserId.HasValue && recordOwnerUserId.Value != OwnerUserId.Value)
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(ChannelId)
            && !string.IsNullOrWhiteSpace(recordChannelId)
            && !string.Equals(recordChannelId, ChannelId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }
}
