using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class CodeRunnerStep : StepBodyAsync
{
    private readonly ICodeExecutionService _codeExecutionService;

    public CodeRunnerStep(ICodeExecutionService codeExecutionService)
    {
        _codeExecutionService = codeExecutionService;
    }

    public string Expression { get; set; } = string.Empty;
    public string OutputKey { get; set; } = "codeResult";
    public int? TimeoutSeconds { get; set; }

    public override async Task<ExecutionResult> RunAsync(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var resolvedExpression = WorkflowStepDataHelper.ResolveTemplate(Expression, data);
        if (string.IsNullOrWhiteSpace(resolvedExpression))
        {
            data[OutputKey] = null;
            return ExecutionResult.Next();
        }

        var request = new CodeExecutionRequest(
            resolvedExpression,
            data,
            TimeoutSeconds ?? 0);
        var result = await _codeExecutionService.ExecuteAsync(request, context.CancellationToken);
        data[OutputKey] = result.Success ? result.Output : result.ErrorMessage;
        data[$"{OutputKey}Meta"] = new
        {
            result.Success,
            result.TimedOut,
            result.DurationMs,
            result.ErrorMessage
        };

        return ExecutionResult.Next();
    }
}
