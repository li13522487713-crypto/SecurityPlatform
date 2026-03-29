using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AgentRepository : RepositoryBase<Agent>
{
    public sealed record ModelConfigReferenceSummary(long TotalCount, IReadOnlyList<Agent> Samples);

    public AgentRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<Agent> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        AgentStatus? status,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || (x.Description != null && x.Description.Contains(keyword)));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<bool> ExistsByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken)
    {
        var count = await Db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name)
            .CountAsync(cancellationToken);
        return count > 0;
    }

    public async Task<ModelConfigReferenceSummary> GetModelConfigReferenceSummaryAsync(
        TenantId tenantId,
        long modelConfigId,
        int sampleSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ModelConfigId == modelConfigId && x.ModelConfigId > 0);

        var total = await query.CountAsync(cancellationToken);
        if (total <= 0)
        {
            return new ModelConfigReferenceSummary(0, Array.Empty<Agent>());
        }

        var samples = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .Take(Math.Max(1, sampleSize))
            .ToListAsync(cancellationToken);

        return new ModelConfigReferenceSummary(total, samples);
    }
}
