using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

public class WaitFor : StepBody
{
    public string EventKey { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }

    public object? EventData { get; set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        if (!context.ExecutionPointer.EventPublished)
        {
            var effectiveDate = EffectiveDate != default ? EffectiveDate : DateTime.MinValue;
            return ExecutionResult.WaitForEvent(EventName, EventKey, effectiveDate);
        }

        EventData = context.ExecutionPointer.EventData;
        return ExecutionResult.Next();
    }
}
