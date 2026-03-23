using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class OpenApiProjectRepository : RepositoryBase<OpenApiProject>
{
    public OpenApiProjectRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<OpenApiProject> Items, long Total)> GetPagedByOwnerAsync(
        TenantId tenantId,
        long createdByUserId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<OpenApiProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.CreatedByUserId == createdByUserId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || x.AppId.Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<OpenApiProject?> FindOwnedByIdAsync(
        TenantId tenantId,
        long createdByUserId,
        long projectId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<OpenApiProject>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.CreatedByUserId == createdByUserId &&
                x.Id == projectId)
            .FirstAsync(cancellationToken);
    }

    public async Task<OpenApiProject?> FindByAppIdAsync(
        TenantId tenantId,
        string appId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<OpenApiProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
    }
}
