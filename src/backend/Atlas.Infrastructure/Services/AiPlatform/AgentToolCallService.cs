using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentToolCallService : IAgentToolCallService
{
    private readonly AgentPluginBindingRepository _bindingRepository;
    private readonly AiPluginRepository _pluginRepository;
    private readonly IAiPluginService _aiPluginService;

    public AgentToolCallService(
        AgentPluginBindingRepository bindingRepository,
        AiPluginRepository pluginRepository,
        IAiPluginService aiPluginService)
    {
        _bindingRepository = bindingRepository;
        _pluginRepository = pluginRepository;
        _aiPluginService = aiPluginService;
    }

    public async Task<AgentToolCallResult> TryExecuteAsync(
        TenantId tenantId,
        long agentId,
        string userMessage,
        int maxIterations,
        CancellationToken cancellationToken)
    {
        if (maxIterations <= 0 || string.IsNullOrWhiteSpace(userMessage))
        {
            return new AgentToolCallResult(false, null, Array.Empty<AgentToolCallStep>(), null);
        }

        var bindings = await _bindingRepository.GetByAgentIdAsync(tenantId, agentId, cancellationToken);
        var enabledBindings = bindings
            .Where(binding => binding.IsEnabled)
            .OrderBy(binding => binding.SortOrder)
            .ToArray();
        if (enabledBindings.Length == 0)
        {
            return new AgentToolCallResult(false, null, Array.Empty<AgentToolCallStep>(), null);
        }

        var pluginIds = enabledBindings.Select(binding => binding.PluginId).Distinct().ToArray();
        var plugins = await _pluginRepository.QueryByIdsAsync(tenantId, pluginIds, cancellationToken);
        if (plugins.Count == 0)
        {
            return new AgentToolCallResult(false, null, Array.Empty<AgentToolCallStep>(), null);
        }

        var pluginMap = plugins.ToDictionary(plugin => plugin.Id);
        var selectedBinding = SelectBinding(enabledBindings, pluginMap, userMessage);
        if (selectedBinding is null || !pluginMap.TryGetValue(selectedBinding.PluginId, out var selectedPlugin))
        {
            return new AgentToolCallResult(false, null, Array.Empty<AgentToolCallStep>(), null);
        }

        var steps = new List<AgentToolCallStep>
        {
            new("thought", $"分析用户问题，选择调用工具「{selectedPlugin.Name}」。")
        };

        var (apiId, inputJson) = BuildDebugInput(selectedBinding.ToolConfigJson, userMessage);
        steps.Add(new(
            "action",
            JsonSerializer.Serialize(new
            {
                pluginId = selectedPlugin.Id,
                pluginName = selectedPlugin.Name,
                apiId,
                input = inputJson
            })));

        var debugResult = await _aiPluginService.DebugAsync(
            tenantId,
            selectedPlugin.Id,
            new AiPluginDebugRequest(apiId, inputJson),
            cancellationToken);

        if (!debugResult.Success)
        {
            return new AgentToolCallResult(
                true,
                debugResult.ErrorMessage ?? "工具调用失败。",
                steps,
                JsonSerializer.Serialize(new
                {
                    pluginId = selectedPlugin.Id,
                    pluginName = selectedPlugin.Name,
                    success = false,
                    errorMessage = debugResult.ErrorMessage
                }));
        }

        steps.Add(new("observation", debugResult.OutputJson));
        var finalAnswer = $"已通过工具「{selectedPlugin.Name}」执行，结果如下：\n{debugResult.OutputJson}";
        steps.Add(new("final", finalAnswer));

        return new AgentToolCallResult(
            true,
            finalAnswer,
            steps,
            JsonSerializer.Serialize(new
            {
                pluginId = selectedPlugin.Id,
                pluginName = selectedPlugin.Name,
                durationMs = debugResult.DurationMs,
                success = true
            }));
    }

    private static Domain.AiPlatform.Entities.AgentPluginBinding? SelectBinding(
        IReadOnlyList<Domain.AiPlatform.Entities.AgentPluginBinding> enabledBindings,
        IReadOnlyDictionary<long, Domain.AiPlatform.Entities.AiPlugin> pluginMap,
        string userMessage)
    {
        var normalizedMessage = userMessage.Trim();
        foreach (var binding in enabledBindings)
        {
            if (!pluginMap.TryGetValue(binding.PluginId, out var plugin))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(plugin.Name) &&
                normalizedMessage.Contains(plugin.Name, StringComparison.OrdinalIgnoreCase))
            {
                return binding;
            }
        }

        return enabledBindings.FirstOrDefault(binding => pluginMap.ContainsKey(binding.PluginId));
    }

    private static (long? ApiId, string InputJson) BuildDebugInput(string toolConfigJson, string userMessage)
    {
        long? apiId = null;
        string? customInputTemplate = null;

        if (!string.IsNullOrWhiteSpace(toolConfigJson))
        {
            try
            {
                using var configDoc = JsonDocument.Parse(toolConfigJson);
                if (configDoc.RootElement.TryGetProperty("apiId", out var apiIdNode)
                    && apiIdNode.ValueKind == JsonValueKind.Number
                    && apiIdNode.TryGetInt64(out var parsedApiId)
                    && parsedApiId > 0)
                {
                    apiId = parsedApiId;
                }

                if (configDoc.RootElement.TryGetProperty("inputTemplate", out var inputTemplateNode)
                    && inputTemplateNode.ValueKind == JsonValueKind.String)
                {
                    customInputTemplate = inputTemplateNode.GetString();
                }
            }
            catch (JsonException)
            {
                // ignore invalid config and fallback to default payload
            }
        }

        if (!string.IsNullOrWhiteSpace(customInputTemplate))
        {
            var rendered = customInputTemplate!.Replace("{{message}}", userMessage, StringComparison.OrdinalIgnoreCase);
            if (TryNormalizeJson(rendered, out var normalizedCustom))
            {
                return (apiId, normalizedCustom);
            }
        }

        var payload = JsonSerializer.Serialize(new
        {
            query = userMessage,
            timestamp = DateTimeOffset.UtcNow
        });
        return (apiId, payload);
    }

    private static bool TryNormalizeJson(string raw, out string normalized)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            normalized = JsonSerializer.Serialize(doc.RootElement);
            return true;
        }
        catch (JsonException)
        {
            normalized = "{}";
            return false;
        }
    }
}
