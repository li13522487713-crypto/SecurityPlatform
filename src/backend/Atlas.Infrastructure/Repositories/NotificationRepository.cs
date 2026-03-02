using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class NotificationRepository : RepositoryBase<Notification>
{
    public NotificationRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<Notification> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? title,
        string? noticeType,
        bool? isActive,
        int pageIndex,
        int pageSize,
        CancellationToken ct)
    {
        var query = Db.Queryable<Notification>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(title))
            query = query.Where(x => x.Title.Contains(title));
        if (!string.IsNullOrWhiteSpace(noticeType))
            query = query.Where(x => x.NoticeType == noticeType);
        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToPageListAsync(pageIndex, pageSize, ct);

        return (items, total);
    }

    public async Task<List<long>> GetAllUserIdsInTenantAsync(TenantId tenantId, CancellationToken ct)
    {
        return await Db.Queryable<Atlas.Domain.Identity.Entities.UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .Select(x => x.Id)
            .ToListAsync(ct);
    }
}
