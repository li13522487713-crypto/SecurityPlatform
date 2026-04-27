using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Abstractions;

public interface IMicroflowFlowNavigator
{
    Task<MicroflowNavigationResult> NavigateAsync(
        MicroflowExecutionPlan plan,
        MicroflowNavigationOptions options,
        CancellationToken cancellationToken);
}
