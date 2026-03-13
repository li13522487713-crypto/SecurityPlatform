using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiShortcutCommandRepository : RepositoryBase<AiShortcutCommand>
{
    public AiShortcutCommandRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AiShortcutCommand>> GetEnabledAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiShortcutCommand>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsEnabled)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCommandKeyAsync(
        TenantId tenantId,
        string commandKey,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiShortcutCommand>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.CommandKey == commandKey);
        if (excludeId.HasValue && excludeId.Value > 0)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.CountAsync(cancellationToken) > 0;
    }
}
