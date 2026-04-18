using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class RuntimeWorkflowAsyncJobRepository : IRuntimeWorkflowAsyncJobRepository
{
    private readonly ISqlSugarClient _db;
    public RuntimeWorkflowAsyncJobRepository(ISqlSugarClient db) => _db = db;

    public async Task<long> InsertAsync(RuntimeWorkflowAsyncJob job, CancellationToken cancellationToken)
    {
        await _db.Insertable(job).ExecuteCommandAsync(cancellationToken);
        return job.Id;
    }

    public Task<RuntimeWorkflowAsyncJob?> FindByJobIdAsync(TenantId tenantId, string jobId, CancellationToken cancellationToken)
    {
        return _db.Queryable<RuntimeWorkflowAsyncJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobId == jobId)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<bool> UpdateAsync(RuntimeWorkflowAsyncJob job, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(job)
            .Where(x => x.Id == job.Id && x.TenantIdValue == job.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
}
