using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

public interface IApprovalCommunicationRecordRepository
{
    Task<IReadOnlyList<ApprovalCommunicationRecord>> GetByTaskIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken);
}
