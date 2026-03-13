using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class OutputEmitterStep : StepBody
{
    public string? Value { get; set; }
    public string OutputKey { get; set; } = "output";

    public override ExecutionResult Run(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var output = WorkflowStepDataHelper.ResolveTemplate(Value, data);
        data[OutputKey] = output;

        if (!data.TryGetValue("outputs", out var raw) || raw is not Dictionary<string, object?> outputs)
        {
            outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            data["outputs"] = outputs;
        }

        outputs[OutputKey] = output;
        return ExecutionResult.Next();
    }
}
