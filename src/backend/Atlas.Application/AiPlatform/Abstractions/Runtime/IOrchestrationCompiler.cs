using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IOrchestrationCompiler
{
    Task<CompiledOrchestrationPlan?> CompileByKeyAsync(
        TenantId tenantId,
        long appInstanceId,
        string planKey,
        CancellationToken cancellationToken = default);

    Task<CompiledOrchestrationPlan?> CompileByIdAsync(
        TenantId tenantId,
        long planId,
        CancellationToken cancellationToken = default);
}
