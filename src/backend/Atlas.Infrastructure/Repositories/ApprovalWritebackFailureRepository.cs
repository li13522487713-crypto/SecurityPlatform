using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ApprovalWritebackFailureRepository : IApprovalWritebackFailureRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalWritebackFailureRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task InsertAsync(ApprovalWritebackFailure entity, CancellationToken cancellationToken)
    {
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalWritebackFailure entity, CancellationToken cancellationToken)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApprovalWritebackFailure?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalWritebackFailure>()
            .Where(x => x.TenantId == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalWritebackFailure>> GetUnresolvedAsync(TenantId tenantId, int limit, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalWritebackFailure>()
            .Where(x => x.TenantId == tenantId.Value && !x.IsResolved)
            .OrderByDescending(x => x.LastAttemptAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountUnresolvedAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalWritebackFailure>()
            .Where(x => x.TenantId == tenantId.Value && !x.IsResolved)
            .CountAsync(cancellationToken);
    }
}
