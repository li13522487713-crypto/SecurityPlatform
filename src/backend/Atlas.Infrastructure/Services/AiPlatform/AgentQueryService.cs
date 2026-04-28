using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentQueryService : IAgentQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AgentRepository _agentRepository;
    private readonly AgentKnowledgeLinkRepository _linkRepository;
    private readonly AgentPluginBindingRepository _pluginBindingRepository;

    public AgentQueryService(
        AgentRepository agentRepository,
        AgentKnowledgeLinkRepository linkRepository,
        AgentPluginBindingRepository pluginBindingRepository)
    {
        _agentRepository = agentRepository;
        _linkRepository = linkRepository;
        _pluginBindingRepository = pluginBindingRepository;
    }

    public async Task<PagedResult<AgentListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        string? status,
        long? workspaceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var statusFilter = ParseStatus(status);
        var (items, total) = await _agentRepository.GetPagedAsync(
            tenantId,
            keyword,
            statusFilter,
            workspaceId,
            pageIndex,
            pageSize,
            cancellationToken);
        var result = items.Select(MapListItem).ToList();
        return new PagedResult<AgentListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<AgentDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var links = await _linkRepository.GetByAgentIdAsync(tenantId, id, cancellationToken);
        var bindings = await _pluginBindingRepository.GetByAgentIdAsync(tenantId, id, cancellationToken);
        var databaseBindings = ParseDatabaseBindings(entity.DatabaseBindingsJson);
        var variableBindings = ParseVariableBindings(entity.VariableBindingsJson);
        return new AgentDetail(
            entity.Id,
            entity.Name,
            NullIfEmpty(entity.Description),
            NullIfEmpty(entity.AvatarUrl),
            NullIfEmpty(entity.SystemPrompt),
            NullIfEmpty(entity.PersonaMarkdown),
            NullIfEmpty(entity.Goals),
            NullIfEmpty(entity.ReplyLogic),
            NullIfEmpty(entity.OutputFormat),
            NullIfEmpty(entity.Constraints),
            NullIfEmpty(entity.OpeningMessage),
            ParsePresetQuestions(entity.PresetQuestionsJson),
            links.Select(MapKnowledgeBinding).ToArray(),
            databaseBindings,
            variableBindings,
            databaseBindings.Select(item => item.DatabaseId).ToArray(),
            variableBindings.Select(item => item.VariableId).ToArray(),
            NullIfNonPositive(entity.ModelConfigId),
            NullIfEmpty(entity.ModelName),
            NullIfZero(entity.Temperature),
            NullIfNonPositive(entity.MaxTokens),
            NullIfNonPositive(entity.DefaultWorkflowId),
            NullIfEmpty(entity.DefaultWorkflowName),
            entity.Status.ToString(),
            entity.CreatorId,
            entity.CreatedAt,
            NullIfEpoch(entity.UpdatedAt),
            NullIfEpoch(entity.PublishedAt),
            entity.PublishVersion,
            entity.EnableMemory,
            entity.EnableShortTermMemory,
            entity.EnableLongTermMemory,
            entity.LongTermMemoryTopK,
            links.Select(x => x.KnowledgeBaseId).ToArray(),
            bindings.Select(binding => new AgentPluginBindingItem(
                binding.PluginId,
                binding.SortOrder,
                binding.IsEnabled,
                binding.ToolConfigJson,
                ParsePluginToolBindings(binding.ToolConfigJson))).ToArray(),
            entity.PublishedConnectorConfigJson,
            entity.WorkspaceId);
    }

    private static AgentListItem MapListItem(Agent entity)
        => new(
            entity.Id,
            entity.Name,
            NullIfEmpty(entity.Description),
            NullIfEmpty(entity.AvatarUrl),
            entity.Status.ToString(),
            NullIfEmpty(entity.ModelName),
            entity.CreatedAt,
            entity.PublishVersion);

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static long? NullIfNonPositive(long? value)
        => value.HasValue && value.Value > 0 ? value : null;

    private static int? NullIfNonPositive(int? value)
        => value.HasValue && value.Value > 0 ? value : null;

    private static float? NullIfZero(float? value)
        => value.HasValue && Math.Abs(value.Value) > float.Epsilon ? value : null;

    private static DateTime? NullIfEpoch(DateTime? value)
        => value.HasValue && value.Value > DateTime.UnixEpoch ? value : null;

    private static AgentStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse<AgentStatus>(status, true, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyList<string> ParsePresetQuestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<long> ParseIdList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<long>();
        }

        try
        {
            return JsonSerializer.Deserialize<long[]>(json, JsonOptions) ?? Array.Empty<long>();
        }
        catch
        {
            return Array.Empty<long>();
        }
    }

    private static IReadOnlyList<AgentDatabaseBindingItem> ParseDatabaseBindings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AgentDatabaseBindingItem>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<AgentDatabaseBindingItem>();
            }

            var result = new List<AgentDatabaseBindingItem>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var legacyId) && legacyId > 0)
                {
                    result.Add(new AgentDatabaseBindingItem(legacyId, null, "readonly", Array.Empty<string>(), result.Count == 0));
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var databaseId = TryGetInt64(item, "databaseId");
                if (databaseId <= 0)
                {
                    continue;
                }

                result.Add(new AgentDatabaseBindingItem(
                    databaseId,
                    GetOptionalString(item, "alias"),
                    NormalizeDatabaseAccessMode(GetOptionalString(item, "accessMode")),
                    GetStringArray(item, "tableAllowlist"),
                    TryGetBoolean(item, "isDefault")));
            }

            return result;
        }
        catch
        {
            return ParseIdList(json)
                .Select((id, index) => new AgentDatabaseBindingItem(id, null, "readonly", Array.Empty<string>(), index == 0))
                .ToArray();
        }
    }

    private static IReadOnlyList<AgentVariableBindingItem> ParseVariableBindings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AgentVariableBindingItem>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<AgentVariableBindingItem>();
            }

            var result = new List<AgentVariableBindingItem>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var legacyId) && legacyId > 0)
                {
                    result.Add(new AgentVariableBindingItem(legacyId, null, false, null));
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var variableId = TryGetInt64(item, "variableId");
                if (variableId <= 0)
                {
                    continue;
                }

                result.Add(new AgentVariableBindingItem(
                    variableId,
                    GetOptionalString(item, "alias"),
                    TryGetBoolean(item, "isRequired"),
                    GetOptionalString(item, "defaultValueOverride")));
            }

            return result;
        }
        catch
        {
            return ParseIdList(json)
                .Select(id => new AgentVariableBindingItem(id, null, false, null))
                .ToArray();
        }
    }

    private static AgentKnowledgeBindingItem MapKnowledgeBinding(AgentKnowledgeLink link)
        => new(
            link.KnowledgeBaseId,
            link.IsEnabled,
            link.InvokeMode,
            link.TopK,
            link.ScoreThreshold,
            ParseStringList(link.EnabledContentTypesJson),
            link.RewriteQueryTemplate);

    private static IReadOnlyList<AgentPluginToolBindingItem> ParsePluginToolBindings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AgentPluginToolBindingItem>();
        }

        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            var items = root?["toolBindings"]?.AsArray();
            if (items is null)
            {
                return Array.Empty<AgentPluginToolBindingItem>();
            }

            return items
                .OfType<JsonObject>()
                .Select(item => new AgentPluginToolBindingItem(
                    item["apiId"]?.GetValue<long>() ?? 0,
                    item["isEnabled"]?.GetValue<bool>() ?? true,
                    item["timeoutSeconds"]?.GetValue<int>() ?? 30,
                    item["failurePolicy"]?.GetValue<string>() ?? "fail",
                    item["parameterBindings"] is JsonArray parameterItems
                        ? parameterItems
                            .OfType<JsonObject>()
                            .Select(parameter => new AgentPluginParameterBindingItem(
                                parameter["parameterName"]?.GetValue<string>() ?? string.Empty,
                                parameter["valueSource"]?.GetValue<string>() ?? "literal",
                                parameter["literalValue"]?.GetValue<string>(),
                                parameter["variableKey"]?.GetValue<string>()))
                            .ToArray()
                        : Array.Empty<AgentPluginParameterBindingItem>()))
                .Where(item => item.ApiId > 0)
                .ToArray();
        }
        catch
        {
            return Array.Empty<AgentPluginToolBindingItem>();
        }
    }

    private static IReadOnlyList<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ["text", "table", "image"];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? ["text", "table", "image"];
        }
        catch
        {
            return ["text", "table", "image"];
        }
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? NullIfEmpty(property.GetString())
            : null;

    private static bool TryGetBoolean(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : false;

    private static long TryGetInt64(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var result)
            ? result
            : 0;

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeDatabaseAccessMode(string? value)
        => string.Equals(value, "readwrite", StringComparison.OrdinalIgnoreCase) ? "readwrite" : "readonly";
}
