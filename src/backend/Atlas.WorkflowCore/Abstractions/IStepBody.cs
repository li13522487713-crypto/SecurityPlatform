using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

public interface IStepBody
{
    Task<ExecutionResult> RunAsync(IStepExecutionContext context);
}
