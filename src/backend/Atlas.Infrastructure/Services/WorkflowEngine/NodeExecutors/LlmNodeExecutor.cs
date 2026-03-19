using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// LLM 调用节点：通过 ILlmProviderFactory 调用大语言模型。
/// Config 参数：prompt, model, provider, temperature, maxTokens, outputKey
/// </summary>
public sealed class LlmNodeExecutor : INodeExecutor
{
    private readonly ILlmProviderFactory _llmProviderFactory;

    public LlmNodeExecutor(ILlmProviderFactory llmProviderFactory)
    {
        _llmProviderFactory = llmProviderFactory;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.Llm;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var promptTemplate = context.GetConfigString("prompt");
        var model = context.GetConfigString("model", "gpt-4o-mini");
        var provider = context.GetConfigString("provider");
        var outputKey = context.GetConfigString("outputKey", "llm_output");

        // 变量替换
        var prompt = context.ReplaceVariables(promptTemplate);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            outputs[outputKey] = VariableResolver.CreateStringElement(string.Empty);
            return new NodeExecutionResult(true, outputs);
        }

        try
        {
            var llmProvider = _llmProviderFactory.GetLlmProvider(provider);

            float.TryParse(context.GetConfigString("temperature"), NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature);
            int.TryParse(context.GetConfigString("maxTokens"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxTokens);

            var request = new ChatCompletionRequest(
                model,
                [new ChatMessage("user", prompt)],
                temperature > 0 ? temperature : null,
                maxTokens > 0 ? maxTokens : null,
                provider);

            var result = await llmProvider.ChatAsync(request, cancellationToken);
            outputs[outputKey] = VariableResolver.CreateStringElement(result.Content);

            await context.EmitEventAsync("llm_output", result.Content, cancellationToken);

            return new NodeExecutionResult(true, outputs);
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult(false, outputs, $"LLM 调用失败: {ex.Message}");
        }
    }
}
