using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class UserAccountRepository : RepositoryBase<UserAccount>, IUserAccountRepository
{
    public UserAccountRepository(ISqlSugarClient db) : base(db) { }

    public Task UpdateRangeAsync(IReadOnlyList<UserAccount> accounts, CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Updateable(accounts.ToList())
            .WhereColumns(x => new { x.Id, x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<UserAccount?> FindByUsernameAsync(TenantId tenantId, string username, CancellationToken cancellationToken)
    {
        var query = Db.Queryable<UserAccount>()
            .IgnoreColumns(x => new
            {
                x.LockoutEndAt,
                x.ManualLockAt,
                x.LastPasswordChangeAt,
                x.LastLoginAt
            })
            .Where(x => x.TenantIdValue == tenantId.Value && x.Username == username);
        var result = await query.FirstAsync(cancellationToken);
        return result;
    }

    public async Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Username.Contains(keyword) || x.DisplayName.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> QueryPageByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return (Array.Empty<UserAccount>(), 0);
        }

        var idArray = userIds.Distinct().ToArray();
        var query = Db.Queryable<UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.Id));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Username.Contains(keyword) || x.DisplayName.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<bool> ExistsByUsernameAsync(TenantId tenantId, string username, CancellationToken cancellationToken)
    {
        var count = await Db.Queryable<UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Username == username)
            .CountAsync(cancellationToken);
        return count > 0;
    }
}
