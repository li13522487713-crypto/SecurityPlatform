using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class PersonalAccessTokenRepository : RepositoryBase<PersonalAccessToken>
{
    public PersonalAccessTokenRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<PersonalAccessToken> Items, long Total)> GetPagedByOwnerAsync(
        TenantId tenantId,
        long createdByUserId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<PersonalAccessToken>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.CreatedByUserId == createdByUserId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || x.TokenPrefix.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<PersonalAccessToken?> FindOwnedByIdAsync(
        TenantId tenantId,
        long createdByUserId,
        long tokenId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<PersonalAccessToken>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.CreatedByUserId == createdByUserId &&
                x.Id == tokenId)
            .FirstAsync(cancellationToken);
    }

    public async Task<PersonalAccessToken?> FindByHashAsync(
        TenantId tenantId,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<PersonalAccessToken>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TokenHash == tokenHash)
            .FirstAsync(cancellationToken);
    }
}
