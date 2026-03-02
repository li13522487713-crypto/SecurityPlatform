using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IProcessMonitorService
{
    Task<ProcessMonitorDashboard> GetDashboardAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<ProcessInstanceTrace?> GetInstanceTraceAsync(TenantId tenantId, long instanceId, CancellationToken cancellationToken = default);
}
