using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class NotificationService : INotificationQueryService, INotificationCommandService
{
    private readonly NotificationRepository _notificationRepository;
    private readonly UserNotificationRepository _userNotificationRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;

    public NotificationService(
        NotificationRepository notificationRepository,
        UserNotificationRepository userNotificationRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider)
    {
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
    }

    // ===== Query =====

    public async Task<PagedResult<NotificationDto>> GetPagedAsync(
        TenantId tenantId, NotificationPagedQuery query, CancellationToken ct = default)
    {
        var (items, total) = await _notificationRepository.GetPagedAsync(
            tenantId, query.Title, query.NoticeType, query.IsActive,
            query.PageIndex, query.PageSize, ct);

        var dtos = items.Select(ToDto).ToList();
        return new PagedResult<NotificationDto>(dtos, total, query.PageIndex, query.PageSize);
    }

    public async Task<NotificationDto?> GetByIdAsync(TenantId tenantId, long id, CancellationToken ct = default)
    {
        var entity = await _notificationRepository.FindByIdAsync(tenantId, id, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<PagedResult<UserNotificationDto>> GetUserNotificationsAsync(
        TenantId tenantId, long userId, UserNotificationPagedQuery query, CancellationToken ct = default)
    {
        var (items, total) = await _userNotificationRepository.GetUserPagedAsync(
            tenantId, userId, query.IsRead, query.PageIndex, query.PageSize, ct);

        var dtos = items.Select(x => new UserNotificationDto(
            x.Un.Id,
            x.N.Id,
            x.N.Title,
            x.N.Content,
            x.N.NoticeType,
            x.N.Priority,
            x.N.PublishedAt,
            x.Un.IsRead,
            x.Un.ReadAt
        )).ToList();

        return new PagedResult<UserNotificationDto>(dtos, total, query.PageIndex, query.PageSize);
    }

    public async Task<int> GetUnreadCountAsync(TenantId tenantId, long userId, CancellationToken ct = default)
    {
        return await _userNotificationRepository.CountUnreadAsync(tenantId, userId, ct);
    }

    // ===== Command =====

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long publisherId,
        string publisherName,
        NotificationCreateRequest request,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var id = _idGeneratorAccessor.NextId();

        var notification = new Notification(
            tenantId, request.Title, request.Content, request.NoticeType,
            request.Priority, publisherId, publisherName, now, request.ExpiresAt, id);

        await _notificationRepository.AddAsync(notification, ct);

        // 推送给租户内所有用户
        var userIds = await _notificationRepository.GetAllUserIdsInTenantAsync(tenantId, ct);
        var userNotifications = userIds.Select(uid =>
            new UserNotification(tenantId, uid, id, _idGeneratorAccessor.NextId())
        ).ToList();

        await _userNotificationRepository.BulkInsertAsync(userNotifications, ct);

        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId, long id, NotificationUpdateRequest request, CancellationToken ct = default)
    {
        var entity = await _notificationRepository.FindByIdAsync(tenantId, id, ct)
            ?? throw new BusinessException("公告不存在。", ErrorCodes.NotFound);

        entity.Update(request.Title, request.Content, request.NoticeType, request.Priority, request.ExpiresAt);
        await _notificationRepository.UpdateAsync(entity, ct);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken ct = default)
    {
        var entity = await _notificationRepository.FindByIdAsync(tenantId, id, ct)
            ?? throw new BusinessException("公告不存在。", ErrorCodes.NotFound);

        await _userNotificationRepository.DeleteByNotificationIdAsync(tenantId, id, ct);
        await _notificationRepository.DeleteAsync(tenantId, id, ct);
    }

    public async Task MarkReadAsync(TenantId tenantId, long userId, long notificationId, CancellationToken ct = default)
    {
        var un = await _userNotificationRepository.FindByUserAndNotificationAsync(tenantId, userId, notificationId, ct);
        if (un is null || un.IsRead) return;

        un.MarkRead(_timeProvider.GetUtcNow());
        await _userNotificationRepository.UpdateAsync(un, ct);
    }

    public async Task MarkAllReadAsync(TenantId tenantId, long userId, CancellationToken ct = default)
    {
        var unread = await _userNotificationRepository.GetUnreadByUserAsync(tenantId, userId, ct);
        if (unread.Count == 0) return;

        var now = _timeProvider.GetUtcNow();
        foreach (var un in unread) un.MarkRead(now);

        await _userNotificationRepository.BulkUpdateAsync(unread, ct);
    }

    // ===== Mapping =====

    private static NotificationDto ToDto(Notification e) => new(
        e.Id, e.Title, e.Content, e.NoticeType, e.Priority,
        e.PublisherId, e.PublisherName, e.PublishedAt, e.ExpiresAt,
        e.IsActive, e.CreatedAt);
}
