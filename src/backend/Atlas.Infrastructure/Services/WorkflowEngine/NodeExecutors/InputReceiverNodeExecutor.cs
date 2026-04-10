using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class InputReceiverNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.InputReceiver;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var inputPath = context.GetConfigString("inputPath", "workflow_input");
        if (context.TryResolveVariable(inputPath, out var providedInput))
        {
            outputs["input"] = providedInput.Clone();
            outputs["input_received"] = JsonSerializer.SerializeToElement(true);
            return Task.FromResult(new NodeExecutionResult(true, outputs));
        }

        if (VariableResolver.TryGetConfigValue(context.Node.Config, "outputSchema", out var schema))
        {
            outputs["expected_schema"] = schema.Clone();
        }

        outputs["input_received"] = JsonSerializer.SerializeToElement(false);
        outputs["input_path"] = VariableResolver.CreateStringElement(inputPath);
        return Task.FromResult(new NodeExecutionResult(
            Success: false,
            Outputs: outputs,
            ErrorMessage: "InputReceiver 等待用户输入。",
            InterruptType: InterruptType.QuestionAnswer));
    }
}
