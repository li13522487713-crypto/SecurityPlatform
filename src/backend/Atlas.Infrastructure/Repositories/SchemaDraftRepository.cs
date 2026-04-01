using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class SchemaDraftRepository : ISchemaDraftRepository
{
    private readonly ISqlSugarClient _db;

    public SchemaDraftRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SchemaDraft>> ListByAppInstanceAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SchemaDraft?> FindByIdAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == draftId)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SchemaDraft>> ListPendingByAppAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.AppInstanceId == appInstanceId
                && (x.Status == SchemaDraftStatus.Pending || x.Status == SchemaDraftStatus.Validated))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(SchemaDraft draft, CancellationToken cancellationToken)
    {
        return _db.Insertable(draft).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(SchemaDraft draft, CancellationToken cancellationToken)
    {
        return _db.Updateable(draft).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<SchemaDraft> drafts, CancellationToken cancellationToken)
    {
        if (drafts.Count == 0)
        {
            return;
        }

        await _db.Updateable(drafts.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
