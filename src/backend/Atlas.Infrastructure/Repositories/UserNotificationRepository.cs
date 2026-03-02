using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserNotificationRepository : RepositoryBase<UserNotification>
{
    public UserNotificationRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<(UserNotification Un, Notification N)> Items, long Total)> GetUserPagedAsync(
        TenantId tenantId,
        long userId,
        bool? isRead,
        int pageIndex,
        int pageSize,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var query = Db.Queryable<UserNotification, Notification>(
            (un, n) => un.NotificationId == n.Id)
            .Where((un, n) =>
                un.TenantIdValue == tenantId.Value &&
                un.UserId == userId &&
                n.IsActive &&
                (n.ExpiresAt == null || n.ExpiresAt > now));

        if (isRead.HasValue)
            query = query.Where((un, n) => un.IsRead == isRead.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending((un, n) => n.PublishedAt)
            .Select((un, n) => new { Un = un, N = n })
            .ToPageListAsync(pageIndex, pageSize, ct);

        return (items.Select(x => (x.Un, x.N)).ToList(), total);
    }

    public async Task<int> CountUnreadAsync(TenantId tenantId, long userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await Db.Queryable<UserNotification, Notification>(
            (un, n) => un.NotificationId == n.Id)
            .Where((un, n) =>
                un.TenantIdValue == tenantId.Value &&
                un.UserId == userId &&
                !un.IsRead &&
                n.IsActive &&
                (n.ExpiresAt == null || n.ExpiresAt > now))
            .CountAsync(ct);
    }

    public async Task<UserNotification?> FindByUserAndNotificationAsync(
        TenantId tenantId, long userId, long notificationId, CancellationToken ct)
    {
        return await Db.Queryable<UserNotification>()
            .Where(x => x.TenantIdValue == tenantId.Value &&
                        x.UserId == userId &&
                        x.NotificationId == notificationId)
            .FirstAsync(ct);
    }

    public async Task<List<UserNotification>> GetUnreadByUserAsync(
        TenantId tenantId, long userId, CancellationToken ct)
    {
        return await Db.Queryable<UserNotification>()
            .Where(x => x.TenantIdValue == tenantId.Value &&
                        x.UserId == userId &&
                        !x.IsRead)
            .ToListAsync(ct);
    }

    public async Task BulkInsertAsync(IEnumerable<UserNotification> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        await Db.Insertable(list).ExecuteCommandAsync(ct);
    }

    public async Task BulkUpdateAsync(IEnumerable<UserNotification> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        await Db.Updateable(list).ExecuteCommandAsync(ct);
    }

    public async Task DeleteByNotificationIdAsync(TenantId tenantId, long notificationId, CancellationToken ct)
    {
        await Db.Deleteable<UserNotification>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.NotificationId == notificationId)
            .ExecuteCommandAsync(ct);
    }
}
