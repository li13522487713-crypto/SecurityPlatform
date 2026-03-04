using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ApprovalCommunicationRecordRepository : IApprovalCommunicationRecordRepository
{
    private readonly ISqlSugarClient _db;

    public ApprovalCommunicationRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApprovalCommunicationRecord>> GetByTaskIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApprovalCommunicationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TaskId == taskId)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }
}
