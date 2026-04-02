using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface IFlowCompiler
{
    Task<PhysicalDagPlan> CompileAsync(long flowId, TenantId tenantId, CancellationToken cancellationToken);
}
