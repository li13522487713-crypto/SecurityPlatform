using Atlas.WorkflowCore.Abstractions;

namespace Atlas.WorkflowCore.Models;

public abstract class WorkflowStep
{
    public abstract Type BodyType { get; }

    public virtual int Id { get; set; }

    public virtual string Name { get; set; } = string.Empty;

    public virtual string? ExternalId { get; set; }

    public virtual List<int> Children { get; set; } = new();

    public virtual List<IStepOutcome> Outcomes { get; set; } = new();

    public virtual List<IStepParameter> Inputs { get; set; } = new();

    public virtual List<IStepParameter> Outputs { get; set; } = new();

    public virtual WorkflowErrorHandling? ErrorBehavior { get; set; }

    public virtual TimeSpan? RetryInterval { get; set; }

    public virtual int? CompensationStepId { get; set; }

    public virtual bool ResumeChildrenAfterCompensation => true;

    public virtual bool RevertChildrenAfterCompensation => false;

    public virtual bool ProceedOnCancel { get; set; } = false;

    public virtual ExecutionPipelineDirective InitForExecution(WorkflowExecutorResult executorResult, WorkflowDefinition definition, WorkflowInstance workflow, ExecutionPointer executionPointer)
    {
        return ExecutionPipelineDirective.Next;
    }

    public virtual ExecutionPipelineDirective BeforeExecute(WorkflowExecutorResult executorResult, IStepExecutionContext context, ExecutionPointer executionPointer, IStepBody body)
    {
        return ExecutionPipelineDirective.Next;
    }

    public virtual void AfterExecute(WorkflowExecutorResult executorResult, IStepExecutionContext context, ExecutionResult stepResult, ExecutionPointer executionPointer)
    {
    }

    public virtual void PrimeForRetry(ExecutionPointer pointer)
    {
    }

    public virtual void AfterWorkflowIteration(WorkflowExecutorResult executorResult, WorkflowDefinition definition, WorkflowInstance workflow, ExecutionPointer executionPointer)
    {
    }

    public virtual IStepBody ConstructBody(IServiceProvider serviceProvider)
    {
        var body = serviceProvider.GetService(BodyType) as IStepBody;
        if (body == null)
        {
            var stepCtor = BodyType.GetConstructor(Array.Empty<Type>());
            if (stepCtor != null)
            {
                body = stepCtor.Invoke(null) as IStepBody;
            }
        }
        return body ?? throw new InvalidOperationException($"Cannot construct step body of type {BodyType.Name}");
    }
}

public class WorkflowStep<TStepBody> : WorkflowStep
    where TStepBody : IStepBody
{
    public override Type BodyType => typeof(TStepBody);
}

public enum ExecutionPipelineDirective
{
    Next = 0,
    Defer = 1,
    EndWorkflow = 2
}

public enum WorkflowErrorHandling
{
    Retry = 0,
    Suspend = 1,
    Terminate = 2,
    Compensate = 3
}

public class WorkflowExecutorResult
{
    public bool IsComplete { get; set; }

    public List<ExecutionPointer> ExecutionPointers { get; set; } = new();

    public List<ExecutionError> Errors { get; set; } = new();
}
