using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

public class Activity : StepBody
{
    public string ActivityName { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }

    public object? Parameters { get; set; }

    public object? Result { get; set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        if (!context.ExecutionPointer.EventPublished)
        {
            var effectiveDate = EffectiveDate != default ? EffectiveDate : DateTime.MinValue;
            return ExecutionResult.WaitForActivity(ActivityName, Parameters, effectiveDate);
        }

        Result = context.ExecutionPointer.EventData;
        return ExecutionResult.Next();
    }
}
