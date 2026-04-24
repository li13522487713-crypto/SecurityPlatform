using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 意图识别节点：将输入文本分类到配置的意图列表中。
/// </summary>
public sealed class IntentDetectorNodeExecutor : INodeExecutor
{
    private readonly ILlmProviderFactory _llmProviderFactory;

    public IntentDetectorNodeExecutor(ILlmProviderFactory llmProviderFactory)
    {
        _llmProviderFactory = llmProviderFactory;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.IntentDetector;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var intents = ResolveIntents(context.Node.Config);
        if (intents.Count == 0)
        {
            return new NodeExecutionResult(false, outputs, "IntentDetector 未配置意图列表。");
        }

        var inputTemplate = context.GetConfigString("input");
        if (string.IsNullOrWhiteSpace(inputTemplate))
        {
            inputTemplate = context.GetConfigString("text", "{{query}}");
        }

        var inputText = context.ReplaceVariables(inputTemplate);
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return new NodeExecutionResult(false, outputs, "IntentDetector 输入文本为空。");
        }

        var model = context.GetConfigString(
            "model.modelName",
            context.GetConfigString("modelName", context.GetConfigString("model", "gpt-4o-mini")));
        var provider = context.GetConfigString("provider");
        var modelConfig = await CozeModelConfigResolver.ResolveAsync(context, cancellationToken);
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
        }

        var systemPrompt = context.GetConfigString(
            "systemPrompt",
            modelConfig?.SystemPrompt ?? "你是意图分类器。只输出 JSON：{\"intent\":\"...\",\"confidence\":0.0~1.0,\"reason\":\"...\"}");

        var prompt = BuildPrompt(systemPrompt, inputText, intents);
        try
        {
            var llmProvider = modelConfig is null
                ? _llmProviderFactory.GetLlmProvider(provider)
                : _llmProviderFactory.GetLlmProviderByModelConfigId(modelConfig.Id);
            float.TryParse(
                context.GetConfigString("temperature", modelConfig?.Temperature?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var temperature);
            var request = new ChatCompletionRequest(
                model,
                [new ChatMessage("user", prompt)],
                temperature > 0 ? temperature : 0,
                MaxTokens: modelConfig?.MaxTokens ?? 256,
                Provider: provider);
            var result = await llmProvider.ChatAsync(request, cancellationToken);

            var parsed = ParseResult(result.Content, intents);
            outputs["detected_intent"] = VariableResolver.CreateStringElement(parsed.Intent);
            outputs["confidence"] = JsonSerializer.SerializeToElement(parsed.Confidence);
            outputs["intent_reason"] = VariableResolver.CreateStringElement(parsed.Reason);
            outputs["intent_raw"] = VariableResolver.CreateStringElement(result.Content);
            return new NodeExecutionResult(true, outputs);
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult(false, outputs, $"IntentDetector 调用失败: {ex.Message}");
        }
    }

    private static List<string> ResolveIntents(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (VariableResolver.TryGetConfigValue(config, "intents", out var intentsRaw))
        {
            if (intentsRaw.ValueKind == JsonValueKind.Array)
            {
                return intentsRaw.EnumerateArray()
                    .Select(x => VariableResolver.ToDisplayText(x).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var text = VariableResolver.ToDisplayText(intentsRaw);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        return [];
    }

    private static string BuildPrompt(string systemPrompt, string input, IReadOnlyList<string> intents)
    {
        return $"{systemPrompt}\n\n意图候选列表：{string.Join(", ", intents)}\n用户输入：{input}\n输出 JSON：";
    }

    private static (string Intent, double Confidence, string Reason) ParseResult(string content, IReadOnlyList<string> intents)
    {
        var fallbackIntent = intents[0];
        var fallbackReason = "LLM 输出无法解析，已回退默认意图。";
        if (string.IsNullOrWhiteSpace(content))
        {
            return (fallbackIntent, 0d, fallbackReason);
        }

        var trimmed = content.Trim();
        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            var directMatch = intents.FirstOrDefault(x => trimmed.Contains(x, StringComparison.OrdinalIgnoreCase));
            return (directMatch ?? fallbackIntent, 0.5d, "使用文本匹配意图回退。");
        }

        var json = trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var intent = root.TryGetProperty("intent", out var intentValue)
                ? VariableResolver.ToDisplayText(intentValue)
                : fallbackIntent;
            if (string.IsNullOrWhiteSpace(intent))
            {
                intent = fallbackIntent;
            }

            if (!intents.Contains(intent, StringComparer.OrdinalIgnoreCase))
            {
                var first = intents.FirstOrDefault(x => intent.Contains(x, StringComparison.OrdinalIgnoreCase));
                intent = first ?? fallbackIntent;
            }

            var confidence = 0.8d;
            if (root.TryGetProperty("confidence", out var confidenceValue) &&
                double.TryParse(VariableResolver.ToDisplayText(confidenceValue), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedConfidence))
            {
                confidence = Math.Clamp(parsedConfidence, 0d, 1d);
            }

            var reason = root.TryGetProperty("reason", out var reasonValue)
                ? VariableResolver.ToDisplayText(reasonValue)
                : string.Empty;
            return (intent, confidence, reason);
        }
        catch
        {
            return (fallbackIntent, 0d, fallbackReason);
        }
    }
}
