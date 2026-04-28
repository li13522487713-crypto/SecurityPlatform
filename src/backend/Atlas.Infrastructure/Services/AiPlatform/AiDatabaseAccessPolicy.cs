using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// D2：基于 <see cref="AiDatabase.QueryMode"/> 与 <see cref="AiDatabase.ChannelScope"/> 的访问策略。
/// 老记录 OwnerUserId/ChannelId 为空时一律视为全员可见（兼容旧数据）。
/// </summary>
public sealed record AiDatabaseAccessPolicy(long? OwnerUserId, string? CurrentChannelId, AiDatabaseChannelScope ChannelScope)
{
    /// <summary>与 <see cref="CurrentChannelId"/> 同义（工作流/节点侧兼容名）。</summary>
    public string? ChannelId => CurrentChannelId;

    private static readonly HashSet<string> InternalSharedChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "chat-sdk",
        "web-sdk",
        "coze-store",
        "template",
        "coze",
        "console",
        "web-console"
    };

    public static AiDatabaseAccessPolicy Open { get; } = new(null, null, AiDatabaseChannelScope.FullShared);

    public static AiDatabaseAccessPolicy For(AiDatabase database, long? userId, string? channelId)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (database.QueryMode == AiDatabaseQueryMode.SingleUser && !userId.HasValue)
        {
            throw new BusinessException(
                "当前数据库为单用户隔离模式，缺少用户上下文，拒绝访问。",
                ErrorCodes.ValidationError);
        }

        if (database.ChannelScope != AiDatabaseChannelScope.FullShared && string.IsNullOrWhiteSpace(channelId))
        {
            throw new BusinessException(
                "当前数据库为渠道隔离模式，缺少渠道上下文（例如 X-App-Channel），拒绝访问。",
                ErrorCodes.ValidationError);
        }

        var owner = database.QueryMode == AiDatabaseQueryMode.SingleUser ? userId : null;
        var channel = string.IsNullOrWhiteSpace(channelId) ? null : channelId.Trim();
        return new AiDatabaseAccessPolicy(owner, channel, database.ChannelScope);
    }

    public bool IsRecordVisible(long? recordOwnerUserId, string? recordChannelId)
    {
        if (OwnerUserId.HasValue && recordOwnerUserId.HasValue && recordOwnerUserId.Value != OwnerUserId.Value)
        {
            return false;
        }

        if (ChannelScope == AiDatabaseChannelScope.FullShared || string.IsNullOrWhiteSpace(CurrentChannelId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(recordChannelId))
        {
            return true;
        }

        if (ChannelScope == AiDatabaseChannelScope.ChannelIsolated)
        {
            return string.Equals(recordChannelId, CurrentChannelId, StringComparison.OrdinalIgnoreCase);
        }

        if (ChannelScope == AiDatabaseChannelScope.InternalShared)
        {
            var currentIsInternal = InternalSharedChannels.Contains(CurrentChannelId);
            var recordIsInternal = InternalSharedChannels.Contains(recordChannelId);
            if (currentIsInternal)
            {
                return recordIsInternal;
            }

            return string.Equals(recordChannelId, CurrentChannelId, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}
