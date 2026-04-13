using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using System.Text.Json;

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
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var statusFilter = ParseStatus(status);
        var (items, total) = await _agentRepository.GetPagedAsync(
            tenantId,
            keyword,
            statusFilter,
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
            ParseIdList(entity.DatabaseBindingsJson),
            ParseIdList(entity.VariableBindingsJson),
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
                binding.ToolConfigJson)).ToArray());
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
}
