using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

public class WorkflowStepInline : WorkflowStep<InlineStepBody>
{
    public Func<IStepExecutionContext, ExecutionResult>? Body { get; set; }

    public override IStepBody ConstructBody(IServiceProvider serviceProvider)
    {
        if (Body == null)
        {
            throw new InvalidOperationException("Body function is not set");
        }
        return new InlineStepBody(Body);
    }
}
