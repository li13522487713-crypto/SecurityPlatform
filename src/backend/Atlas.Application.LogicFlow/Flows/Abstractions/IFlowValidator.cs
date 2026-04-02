using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface IFlowValidator
{
    Task<FlowValidationResult> ValidateAsync(long flowId, TenantId tenantId, CancellationToken cancellationToken);
}
