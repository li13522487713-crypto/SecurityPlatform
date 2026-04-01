using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.SemanticKernel;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentKernelAugmentationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentPluginBindingRepository _agentPluginBindingRepository;
    private readonly AiPluginRepository _aiPluginRepository;
    private readonly AiPluginApiRepository _aiPluginApiRepository;
    private readonly AgentKnowledgeLinkRepository _agentKnowledgeLinkRepository;
    private readonly IRagRetrievalService _ragRetrievalService;
    private readonly AiPluginRuntimeExecutor _pluginRuntimeExecutor;

    public AgentKernelAugmentationService(
        AgentPluginBindingRepository agentPluginBindingRepository,
        AiPluginRepository aiPluginRepository,
        AiPluginApiRepository aiPluginApiRepository,
        AgentKnowledgeLinkRepository agentKnowledgeLinkRepository,
        IRagRetrievalService ragRetrievalService,
        AiPluginRuntimeExecutor pluginRuntimeExecutor)
    {
        _agentPluginBindingRepository = agentPluginBindingRepository;
        _aiPluginRepository = aiPluginRepository;
        _aiPluginApiRepository = aiPluginApiRepository;
        _agentKnowledgeLinkRepository = agentKnowledgeLinkRepository;
        _ragRetrievalService = ragRetrievalService;
        _pluginRuntimeExecutor = pluginRuntimeExecutor;
    }

    public async Task<AgentKernelAugmentationResult> ConfigureAsync(
        TenantId tenantId,
        long agentId,
        Kernel kernel,
        bool enableRag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        var boundFunctionCount = await ConfigurePluginFunctionsAsync(tenantId, agentId, kernel, cancellationToken);
        var knowledgePluginEnabled = false;
        if (enableRag)
        {
            knowledgePluginEnabled = await ConfigureKnowledgePluginAsync(tenantId, agentId, kernel, cancellationToken);
        }

        return new AgentKernelAugmentationResult(boundFunctionCount, knowledgePluginEnabled);
    }

    private async Task<int> ConfigurePluginFunctionsAsync(
        TenantId tenantId,
        long agentId,
        Kernel kernel,
        CancellationToken cancellationToken)
    {
        var bindings = await _agentPluginBindingRepository.GetByAgentIdAsync(tenantId, agentId, cancellationToken);
        var enabledBindings = bindings
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.SortOrder)
            .ToList();
        if (enabledBindings.Count == 0)
        {
            return 0;
        }

        var pluginIds = enabledBindings.Select(item => item.PluginId).Distinct().ToArray();
        var plugins = await _aiPluginRepository.QueryByIdsAsync(tenantId, pluginIds, cancellationToken);
        if (plugins.Count == 0)
        {
            return 0;
        }

        var pluginMap = plugins.ToDictionary(item => item.Id);
        var apis = await _aiPluginApiRepository.GetByPluginIdsAsync(tenantId, pluginIds, cancellationToken);
        var apisByPluginId = apis
            .Where(item => item.IsEnabled)
            .GroupBy(item => item.PluginId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var totalFunctions = 0;
        foreach (var binding in enabledBindings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pluginMap.TryGetValue(binding.PluginId, out var plugin))
            {
                continue;
            }

            if (!apisByPluginId.TryGetValue(plugin.Id, out var pluginApis) || pluginApis.Count == 0)
            {
                continue;
            }

            var bindingConfig = ParseJsonObject(binding.ToolConfigJson);
            var selectedApis = FilterApis(pluginApis, bindingConfig);
            if (selectedApis.Count == 0)
            {
                continue;
            }

            var toolSchemas = BuildToolSchemaMap(plugin.ToolSchemaJson);
            var functions = selectedApis
                .Select(api => CreatePluginFunction(tenantId, plugin, api, toolSchemas))
                .ToList();
            if (functions.Count == 0)
            {
                continue;
            }

            var pluginName = SanitizePluginName(plugin.Name, plugin.Id);
            kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions(
                pluginName,
                plugin.Description ?? pluginName,
                functions));
            totalFunctions += functions.Count;
        }

        return totalFunctions;
    }

    private async Task<bool> ConfigureKnowledgePluginAsync(
        TenantId tenantId,
        long agentId,
        Kernel kernel,
        CancellationToken cancellationToken)
    {
        var knowledgeLinks = await _agentKnowledgeLinkRepository.GetByAgentIdAsync(tenantId, agentId, cancellationToken);
        var knowledgeBaseIds = knowledgeLinks
            .Select(item => item.KnowledgeBaseId)
            .Distinct()
            .ToArray();
        if (knowledgeBaseIds.Length == 0)
        {
            return false;
        }

        var knowledgeSearchFunction = KernelFunctionFactory.CreateFromMethod(
            method: async (string query, int topK, CancellationToken ct) =>
                await SearchKnowledgeAsync(tenantId, knowledgeBaseIds, query, topK, ct),
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = "search_knowledge",
                Description = "在当前 Agent 绑定的知识库中检索与问题最相关的内容。"
            });

        kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions(
            "knowledge_search",
            "知识库检索插件",
            [knowledgeSearchFunction]));
        return true;
    }

    private KernelFunction CreatePluginFunction(
        TenantId tenantId,
        AiPlugin plugin,
        AiPluginApi api,
        IReadOnlyDictionary<string, JsonObject> toolSchemas)
    {
        toolSchemas.TryGetValue(api.Name, out var toolSchema);
        var parameterMetadata = BuildParameterMetadata(toolSchema);

        return KernelFunctionFactory.CreateFromMethod(
            method: async (KernelArguments arguments, CancellationToken ct) =>
                await ExecutePluginFunctionAsync(tenantId, plugin, api, arguments, ct),
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = api.Name,
                Description = string.IsNullOrWhiteSpace(api.Description) ? $"{api.Method} {api.Path}" : api.Description,
                Parameters = parameterMetadata
            });
    }

    private async Task<string> ExecutePluginFunctionAsync(
        TenantId tenantId,
        AiPlugin plugin,
        AiPluginApi api,
        KernelArguments arguments,
        CancellationToken cancellationToken)
    {
        var inputNode = new JsonObject();
        foreach (var pair in arguments)
        {
            inputNode[pair.Key] = ConvertToJsonNode(pair.Value);
        }

        var executionResult = await _pluginRuntimeExecutor.ExecuteAsync(
            tenantId,
            plugin,
            api,
            inputNode.ToJsonString(JsonOptions),
            cancellationToken);
        if (!executionResult.Success)
        {
            throw new InvalidOperationException(executionResult.ErrorMessage ?? $"插件函数 {api.Name} 执行失败。");
        }

        return executionResult.OutputJson;
    }

    private async Task<string> SearchKnowledgeAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "知识检索参数为空。";
        }

        var results = await _ragRetrievalService.SearchAsync(
            tenantId,
            knowledgeBaseIds,
            query.Trim(),
            topK <= 0 ? 5 : Math.Min(topK, 10),
            cancellationToken);
        if (results.Count == 0)
        {
            return "未检索到相关知识。";
        }

        return string.Join(
            "\n\n",
            results.Select((item, index) =>
                $"[KB#{index + 1}] kb={item.KnowledgeBaseId}, doc={item.DocumentId}, chunk={item.ChunkId}, score={item.Score:F4}\n{item.Content}"));
    }

    private static IReadOnlyList<AiPluginApi> FilterApis(
        IReadOnlyList<AiPluginApi> apis,
        JsonObject bindingConfig)
    {
        var includeApiIds = new HashSet<long>();
        if (bindingConfig["apiId"] is JsonValue apiIdNode && apiIdNode.TryGetValue<long>(out var singleApiId) && singleApiId > 0)
        {
            includeApiIds.Add(singleApiId);
        }

        if (bindingConfig["apiIds"] is JsonArray apiIdsNode)
        {
            foreach (var node in apiIdsNode.OfType<JsonValue>())
            {
                if (node.TryGetValue<long>(out var apiId) && apiId > 0)
                {
                    includeApiIds.Add(apiId);
                }
            }
        }

        var includeFunctionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "includedFunctions", "functionNames", "includedFunctionNames" })
        {
            if (bindingConfig[key] is not JsonArray namesNode)
            {
                continue;
            }

            foreach (var node in namesNode.OfType<JsonValue>())
            {
                if (node.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    includeFunctionNames.Add(name.Trim());
                }
            }
        }

        if (includeApiIds.Count == 0 && includeFunctionNames.Count == 0)
        {
            return apis;
        }

        return apis
            .Where(api => includeApiIds.Contains(api.Id) || includeFunctionNames.Contains(api.Name))
            .ToList();
    }

    private static IReadOnlyDictionary<string, JsonObject> BuildToolSchemaMap(string toolSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(toolSchemaJson))
        {
            return new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            if (JsonNode.Parse(toolSchemaJson) is not JsonArray toolArray)
            {
                return new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
            }

            return toolArray
                .OfType<JsonObject>()
                .Select(toolNode => toolNode["function"] as JsonObject)
                .Where(functionNode => functionNode is not null)
                .ToDictionary(
                    functionNode => functionNode!["name"]?.GetValue<string>() ?? string.Empty,
                    functionNode => functionNode!,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<KernelParameterMetadata> BuildParameterMetadata(JsonObject? toolSchema)
    {
        if (toolSchema?["parameters"] is not JsonObject parametersObject)
        {
            return [];
        }

        var requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (parametersObject["required"] is JsonArray requiredArray)
        {
            foreach (var item in requiredArray.OfType<JsonValue>())
            {
                if (item.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    requiredNames.Add(name.Trim());
                }
            }
        }

        var properties = parametersObject["properties"] as JsonObject;
        if (properties is null || properties.Count == 0)
        {
            return [];
        }

        return properties
            .Select(property =>
            {
                var schema = property.Value as JsonObject;
                var type = schema?["type"]?.GetValue<string>() ?? "string";
                return new KernelParameterMetadata(property.Key)
                {
                    Description = schema?["description"]?.GetValue<string>(),
                    IsRequired = requiredNames.Contains(property.Key),
                    ParameterType = type switch
                    {
                        "integer" => typeof(long),
                        "number" => typeof(double),
                        "boolean" => typeof(bool),
                        "array" => typeof(string),
                        "object" => typeof(string),
                        _ => typeof(string)
                    }
                };
            })
            .ToList();
    }

    private static JsonObject ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static JsonNode? ConvertToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode jsonNode)
        {
            return jsonNode.DeepClone();
        }

        if (value is JsonElement jsonElement)
        {
            return JsonNode.Parse(jsonElement.GetRawText());
        }

        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return JsonValue.Create(text);
            }

            try
            {
                return JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                return JsonValue.Create(text);
            }
        }

        try
        {
            return JsonSerializer.SerializeToNode(value, JsonOptions);
        }
        catch (Exception)
        {
            return JsonValue.Create(value.ToString());
        }
    }

    private static string SanitizePluginName(string pluginName, long pluginId)
    {
        var sanitized = new string(pluginName
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray())
            .Trim('_');

        return string.IsNullOrWhiteSpace(sanitized)
            ? $"plugin_{pluginId}"
            : sanitized;
    }
}

public sealed record AgentKernelAugmentationResult(
    int BoundToolFunctionCount,
    bool KnowledgePluginEnabled);
