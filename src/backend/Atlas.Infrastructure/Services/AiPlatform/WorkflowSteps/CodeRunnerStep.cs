using System.Data;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class CodeRunnerStep : StepBody
{
    public string Expression { get; set; } = string.Empty;
    public string OutputKey { get; set; } = "codeResult";

    public override ExecutionResult Run(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var resolvedExpression = WorkflowStepDataHelper.ResolveTemplate(Expression, data);
        if (string.IsNullOrWhiteSpace(resolvedExpression))
        {
            data[OutputKey] = null;
            return ExecutionResult.Next();
        }

        try
        {
            using var table = new DataTable();
            data[OutputKey] = table.Compute(resolvedExpression, string.Empty);
        }
        catch
        {
            data[OutputKey] = resolvedExpression;
        }

        return ExecutionResult.Next();
    }
}
