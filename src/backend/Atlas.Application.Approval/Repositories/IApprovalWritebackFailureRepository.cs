using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;

namespace Atlas.Application.Approval.Repositories;

public interface IApprovalWritebackFailureRepository
{
    Task InsertAsync(ApprovalWritebackFailure entity, CancellationToken cancellationToken);
    Task UpdateAsync(ApprovalWritebackFailure entity, CancellationToken cancellationToken);
    Task<ApprovalWritebackFailure?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalWritebackFailure>> GetUnresolvedAsync(TenantId tenantId, int limit, CancellationToken cancellationToken);
    Task<int> CountUnresolvedAsync(TenantId tenantId, CancellationToken cancellationToken);
}
