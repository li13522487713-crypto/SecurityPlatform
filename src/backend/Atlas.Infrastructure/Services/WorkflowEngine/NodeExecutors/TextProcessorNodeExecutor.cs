using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 文本处理节点：模板渲染，支持 {{variable}} 变量替换。
/// Config 参数：template、outputKey（默认 "text_output"）
/// </summary>
public sealed class TextProcessorNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.TextProcessor;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var template = context.GetConfigString("template");
        var outputKey = context.GetConfigString("outputKey", "text_output");
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var result = context.ReplaceVariables(template);

        outputs[outputKey] = VariableResolver.CreateStringElement(result);
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
