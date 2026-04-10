using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

public sealed class OutputEmitterNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.OutputEmitter;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var outputKey = context.GetConfigString("outputKey", "output");
        var template = context.GetConfigString("template");
        var rendered = context.ReplaceVariables(template);
        outputs[outputKey] = VariableResolver.CreateStringElement(rendered);
        await context.EmitEventAsync("output", rendered, cancellationToken);
        return new NodeExecutionResult(true, outputs);
    }
}
