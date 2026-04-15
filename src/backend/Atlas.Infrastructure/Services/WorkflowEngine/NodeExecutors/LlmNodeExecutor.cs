using System.Globalization;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Microsoft.Extensions.DependencyInjection;

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
        var systemPromptTemplate = context.GetConfigString("systemPrompt");
        var model = context.GetConfigString("model.modelName", context.GetConfigString("model", "gpt-4o-mini"));
        var provider = context.GetConfigString("provider");
        var outputKey = context.GetConfigString("outputKey", "llm_output");
        var modelConfig = await ResolveModelConfigAsync(context, cancellationToken);
        if (modelConfig is not null)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                provider = modelConfig.ProviderType;
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                model = string.IsNullOrWhiteSpace(modelConfig.ModelId)
                    ? modelConfig.DefaultModel
                    : modelConfig.ModelId;
            }

            if (string.IsNullOrWhiteSpace(systemPromptTemplate))
            {
                systemPromptTemplate = modelConfig.SystemPrompt ?? string.Empty;
            }
        }

        var stream = context.GetConfigBoolean("stream", modelConfig?.EnableStreaming ?? false);

        // 变量替换
        var prompt = context.ReplaceVariables(promptTemplate);
        var systemPrompt = context.ReplaceVariables(systemPromptTemplate);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            outputs[outputKey] = VariableResolver.CreateStringElement(string.Empty);
            return new NodeExecutionResult(true, outputs);
        }

        try
        {
            var llmProvider = _llmProviderFactory.GetLlmProvider(provider);

            float.TryParse(
                context.GetConfigString("temperature", modelConfig?.Temperature?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var temperature);
            int.TryParse(
                context.GetConfigString("maxTokens", modelConfig?.MaxTokens?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var maxTokens);

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new ChatMessage("system", systemPrompt));
            }

            messages.Add(new ChatMessage("user", prompt));
            var request = new ChatCompletionRequest(
                model,
                messages,
                temperature > 0 ? temperature : null,
                maxTokens > 0 ? maxTokens : null,
                provider);

            string content;
            if (stream && context.EventChannel is not null)
            {
                var builder = new StringBuilder();
                await foreach (var chunk in llmProvider.ChatStreamAsync(request, cancellationToken))
                {
                    if (string.IsNullOrEmpty(chunk.ContentDelta))
                    {
                        continue;
                    }

                    builder.Append(chunk.ContentDelta);
                    await context.EmitEventAsync("llm_output", chunk.ContentDelta, cancellationToken);
                }

                content = builder.ToString();
            }
            else
            {
                var result = await llmProvider.ChatAsync(request, cancellationToken);
                content = result.Content ?? string.Empty;
                await context.EmitEventAsync("llm_output", content, cancellationToken);
            }

            outputs[outputKey] = VariableResolver.CreateStringElement(content);

            return new NodeExecutionResult(true, outputs);
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult(false, outputs, $"LLM 调用失败: {ex.Message}");
        }
    }

    private static async Task<ModelConfigDto?> ResolveModelConfigAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var modelType = context.GetConfigInt64("model.modelType", 0L);
        if (modelType <= 0)
        {
            return null;
        }

        var queryService = context.ServiceProvider.GetService<IModelConfigQueryService>();
        if (queryService is null)
        {
            return null;
        }

        return await queryService.GetByIdAsync(context.TenantId, modelType, cancellationToken);
    }
}
