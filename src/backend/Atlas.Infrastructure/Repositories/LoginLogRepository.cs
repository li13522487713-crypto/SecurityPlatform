using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LoginLogRepository : RepositoryBase<LoginLog>
{
    public LoginLogRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<LoginLog> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? username,
        string? ipAddress,
        bool? loginStatus,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<LoginLog>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(x => x.Username.Contains(username));
        if (!string.IsNullOrWhiteSpace(ipAddress))
            query = query.Where(x => x.IpAddress.Contains(ipAddress));
        if (loginStatus.HasValue)
            query = query.Where(x => x.LoginStatus == loginStatus.Value);
        if (from.HasValue)
            query = query.Where(x => x.LoginTime >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.LoginTime <= to.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.LoginTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task DeleteOlderThanAsync(TenantId tenantId, DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        await Db.Deleteable<LoginLog>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.LoginTime < threshold)
            .ExecuteCommandAsync(cancellationToken);
    }
}
