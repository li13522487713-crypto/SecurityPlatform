using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface INotificationQueryService
{
    /// <summary>管理员：分页查询所有公告</summary>
    Task<PagedResult<NotificationDto>> GetPagedAsync(TenantId tenantId, NotificationPagedQuery query, CancellationToken ct = default);

    /// <summary>管理员：按 ID 获取公告</summary>
    Task<NotificationDto?> GetByIdAsync(TenantId tenantId, long id, CancellationToken ct = default);

    /// <summary>用户：获取当前用户的通知（分页）</summary>
    Task<PagedResult<UserNotificationDto>> GetUserNotificationsAsync(TenantId tenantId, long userId, UserNotificationPagedQuery query, CancellationToken ct = default);

    /// <summary>用户：获取未读通知数量</summary>
    Task<int> GetUnreadCountAsync(TenantId tenantId, long userId, CancellationToken ct = default);
}

public interface INotificationCommandService
{
    /// <summary>创建公告并推送给所有当前租户用户</summary>
    Task<long> CreateAsync(TenantId tenantId, long publisherId, string publisherName, NotificationCreateRequest request, CancellationToken ct = default);

    /// <summary>更新公告</summary>
    Task UpdateAsync(TenantId tenantId, long id, NotificationUpdateRequest request, CancellationToken ct = default);

    /// <summary>删除公告（同时删除关联 UserNotification）</summary>
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken ct = default);

    /// <summary>标记单条通知为已读</summary>
    Task MarkReadAsync(TenantId tenantId, long userId, long notificationId, CancellationToken ct = default);

    /// <summary>标记用户所有未读通知为已读</summary>
    Task MarkAllReadAsync(TenantId tenantId, long userId, CancellationToken ct = default);
}
