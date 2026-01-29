using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

public interface IStepExecutor
{
    Task<ExecutionResult> ExecuteStep(IStepExecutionContext context, IStepBody body);
}
