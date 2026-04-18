using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

public interface IRuntimeWorkflowAsyncJobRepository
{
    Task<long> InsertAsync(RuntimeWorkflowAsyncJob job, CancellationToken cancellationToken);
    Task<RuntimeWorkflowAsyncJob?> FindByJobIdAsync(TenantId tenantId, string jobId, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(RuntimeWorkflowAsyncJob job, CancellationToken cancellationToken);
}
