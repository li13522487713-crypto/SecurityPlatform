using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 插件节点：调用插件调试接口执行插件能力。
/// Config 参数：pluginId、apiId、inputJson、outputKey
/// </summary>
public sealed class PluginNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Plugin;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var outputKey = context.GetConfigString("outputKey", "plugin_output");
        var pluginId = context.GetConfigInt64("pluginId");
        if (pluginId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "插件节点缺少有效的 pluginId 配置");
        }

        var apiId = context.GetConfigInt64("apiId");
        var inputJsonTemplate = context.GetConfigString("inputJson", "{}");
        var inputJson = context.ReplaceVariables(inputJsonTemplate);

        if (!TryNormalizeJson(inputJson, out var normalizedInputJson))
        {
            return new NodeExecutionResult(false, outputs, "插件节点 inputJson 不是合法 JSON");
        }

        try
        {
            var pluginService = context.ServiceProvider.GetRequiredService<IAiPluginService>();
            var debugResult = await pluginService.DebugAsync(
                context.TenantId,
                pluginId,
                new AiPluginDebugRequest(
                    apiId > 0 ? apiId : null,
                    normalizedInputJson),
                cancellationToken);

            outputs[outputKey] = VariableResolver.CreateStringElement(debugResult.OutputJson);
            outputs["plugin_duration_ms"] = JsonSerializer.SerializeToElement(debugResult.DurationMs);
            outputs["plugin_debug_success"] = JsonSerializer.SerializeToElement(debugResult.Success);
            outputs["plugin_error_message"] = VariableResolver.CreateStringElement(debugResult.ErrorMessage ?? string.Empty);

            await context.EmitEventAsync("plugin_output", debugResult.OutputJson, cancellationToken);
            return new NodeExecutionResult(true, outputs);
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult(false, outputs, $"插件调用失败: {ex.Message}");
        }
    }

    private static bool TryNormalizeJson(string raw, out string normalized)
    {
        try
        {
            var node = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            normalized = JsonSerializer.Serialize(node.RootElement);
            return true;
        }
        catch (JsonException)
        {
            normalized = string.Empty;
            return false;
        }
    }
}
