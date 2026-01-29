using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services;

public class StepExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteStep(IStepExecutionContext context, IStepBody body)
    {
        return await body.RunAsync(context);
    }
}
