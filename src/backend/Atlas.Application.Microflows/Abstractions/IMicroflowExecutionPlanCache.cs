using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowExecutionPlanCache
{
    ValueTask<MicroflowExecutionPlan> GetOrCreateAsync(
        MicroflowExecutionPlanCacheKey key,
        Func<CancellationToken, Task<MicroflowExecutionPlan>> factory,
        CancellationToken cancellationToken);

    void Invalidate(string resourceId, string? version);
}
