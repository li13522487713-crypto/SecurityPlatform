using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 代码执行节点：当前版本支持简单表达式求值（字符串拼接、变量替换）。
/// 安全沙箱执行（Roslyn Scripting）将在后续版本中实现。
/// Config 参数：code、language（默认 "expression"）、outputKey
/// </summary>
public sealed class CodeRunnerNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.CodeRunner;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var code = context.GetConfigString("code");
        var outputKey = context.GetConfigString("outputKey", "code_output");
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 简单表达式求值：支持变量替换
            var result = context.ReplaceVariables(code);
            outputs[outputKey] = VariableResolver.CreateStringElement(result);
            return Task.FromResult(new NodeExecutionResult(true, outputs));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, $"代码执行失败: {ex.Message}"));
        }
    }
}
