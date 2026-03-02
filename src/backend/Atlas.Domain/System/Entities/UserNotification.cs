using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 用户-通知关联实体（记录每个用户的已读状态）
/// </summary>
public sealed class UserNotification : TenantEntity
{
    public UserNotification()
        : base(TenantId.Empty)
    {
    }

    public UserNotification(
        TenantId tenantId,
        long userId,
        long notificationId,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        NotificationId = notificationId;
        IsRead = false;
    }

    public long UserId { get; private set; }
    public long NotificationId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead(DateTimeOffset now)
    {
        IsRead = true;
        ReadAt = now;
    }
}
