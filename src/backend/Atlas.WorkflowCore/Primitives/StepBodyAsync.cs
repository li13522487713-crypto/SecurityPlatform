using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

public abstract class StepBodyAsync : IStepBody
{
    public abstract Task<ExecutionResult> RunAsync(IStepExecutionContext context);
}
