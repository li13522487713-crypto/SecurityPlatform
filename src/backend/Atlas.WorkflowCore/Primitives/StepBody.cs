using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

public abstract class StepBody : IStepBody
{
    public abstract ExecutionResult Run(IStepExecutionContext context);

    public Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        return Task.FromResult(Run(context));
    }

    protected ExecutionResult OutcomeResult(object? value)
    {
        return new ExecutionResult
        {
            Proceed = true,
            OutcomeValue = value
        };
    }

    protected ExecutionResult PersistResult(object? persistenceData)
    {
        return new ExecutionResult
        {
            Proceed = false,
            PersistenceData = persistenceData
        };
    }

    protected ExecutionResult SleepResult(object? persistenceData, TimeSpan sleep)
    {
        return new ExecutionResult
        {
            Proceed = false,
            PersistenceData = persistenceData,
            SleepFor = sleep
        };
    }
}
