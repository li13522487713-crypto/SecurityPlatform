using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AgentCommandService : IAgentCommandService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AgentRepository _agentRepository;
    private readonly AgentKnowledgeLinkRepository _linkRepository;
    private readonly AgentPluginBindingRepository _pluginBindingRepository;
    private readonly AgentPublicationRepository _publicationRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly AiPluginRepository _pluginRepository;
    private readonly AiPluginApiRepository _pluginApiRepository;
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly AiVariableRepository _variableRepository;
    private readonly ModelConfigRepository _modelConfigRepository;
    private readonly IWorkflowMetaRepository _workflowMetaRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public AgentCommandService(
        AgentRepository agentRepository,
        AgentKnowledgeLinkRepository linkRepository,
        AgentPluginBindingRepository pluginBindingRepository,
        AgentPublicationRepository publicationRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        AiPluginRepository pluginRepository,
        AiPluginApiRepository pluginApiRepository,
        AiDatabaseRepository databaseRepository,
        AiVariableRepository variableRepository,
        ModelConfigRepository modelConfigRepository,
        IWorkflowMetaRepository workflowMetaRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _agentRepository = agentRepository;
        _linkRepository = linkRepository;
        _pluginBindingRepository = pluginBindingRepository;
        _publicationRepository = publicationRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _pluginRepository = pluginRepository;
        _pluginApiRepository = pluginApiRepository;
        _databaseRepository = databaseRepository;
        _variableRepository = variableRepository;
        _modelConfigRepository = modelConfigRepository;
        _workflowMetaRepository = workflowMetaRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long creatorId,
        AgentCreateRequest request,
        CancellationToken cancellationToken)
    {
        var exists = await _agentRepository.ExistsByNameAsync(tenantId, request.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessException("AgentNameExists", ErrorCodes.ValidationError);
        }

        var modelConfigId = await ResolveModelConfigIdAsync(tenantId, request.ModelConfigId, cancellationToken);
        var workflowBinding = await ResolveWorkflowBindingAsync(
            tenantId,
            request.DefaultWorkflowId,
            request.DefaultWorkflowName,
            cancellationToken);
        var entity = new Agent(tenantId, request.Name, creatorId, _idGeneratorAccessor.NextId());
        var knowledgeBindings = await BuildValidatedKnowledgeBindingsAsync(
            tenantId,
            entity.Id,
            request.KnowledgeBindings,
            request.KnowledgeBaseIds,
            cancellationToken);
        var databaseBindings = await BuildValidatedDatabaseBindingsAsync(
            tenantId,
            entity.Id,
            request.DatabaseBindings,
            request.DatabaseBindingIds,
            cancellationToken);
        var variableBindings = await BuildValidatedVariableBindingsAsync(
            tenantId,
            entity.Id,
            request.VariableBindings,
            request.VariableBindingIds,
            cancellationToken);
        entity.Update(
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.SystemPrompt,
            request.PersonaMarkdown,
            request.Goals,
            request.ReplyLogic,
            request.OutputFormat,
            request.Constraints,
            request.OpeningMessage,
            SerializePresetQuestions(request.PresetQuestions),
            SerializeDatabaseBindings(databaseBindings),
            SerializeVariableBindings(variableBindings),
            modelConfigId,
            request.ModelName,
            request.Temperature,
            request.MaxTokens,
            workflowBinding.WorkflowId,
            workflowBinding.WorkflowName,
            request.EnableMemory,
            request.EnableShortTermMemory,
            request.EnableLongTermMemory,
            request.LongTermMemoryTopK);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _agentRepository.AddAsync(entity, cancellationToken);
            await SyncDatabaseBindingsAsync(tenantId, entity.Id, databaseBindings.Select(item => item.DatabaseId).ToArray(), cancellationToken);
            if (knowledgeBindings.Length > 0)
            {
                await _linkRepository.AddRangeAsync(knowledgeBindings, cancellationToken);
            }
        }, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, AgentUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        var modelConfigId = await ResolveModelConfigIdAsync(tenantId, request.ModelConfigId, cancellationToken);
        var workflowBinding = await ResolveWorkflowBindingAsync(
            tenantId,
            request.DefaultWorkflowId,
            request.DefaultWorkflowName,
            cancellationToken);
        var knowledgeBindings = await BuildValidatedKnowledgeBindingsAsync(
            tenantId,
            id,
            request.KnowledgeBindings,
            request.KnowledgeBaseIds,
            cancellationToken);
        var pluginBindings = await BuildValidatedPluginBindingsAsync(
            tenantId,
            id,
            request.PluginBindings,
            cancellationToken);
        var databaseBindings = await BuildValidatedDatabaseBindingsAsync(
            tenantId,
            id,
            request.DatabaseBindings,
            request.DatabaseBindingIds,
            cancellationToken);
        var variableBindings = await BuildValidatedVariableBindingsAsync(
            tenantId,
            id,
            request.VariableBindings,
            request.VariableBindingIds,
            cancellationToken);
        entity.Update(
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.SystemPrompt,
            request.PersonaMarkdown,
            request.Goals,
            request.ReplyLogic,
            request.OutputFormat,
            request.Constraints,
            request.OpeningMessage,
            SerializePresetQuestions(request.PresetQuestions),
            SerializeDatabaseBindings(databaseBindings),
            SerializeVariableBindings(variableBindings),
            modelConfigId,
            request.ModelName,
            request.Temperature,
            request.MaxTokens,
            workflowBinding.WorkflowId,
            workflowBinding.WorkflowName,
            request.EnableMemory,
            request.EnableShortTermMemory,
            request.EnableLongTermMemory,
            request.LongTermMemoryTopK);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _agentRepository.UpdateAsync(entity, cancellationToken);
            await SyncDatabaseBindingsAsync(tenantId, id, databaseBindings.Select(item => item.DatabaseId).ToArray(), cancellationToken);
            await _linkRepository.DeleteByAgentIdAsync(tenantId, id, cancellationToken);
            if (knowledgeBindings.Length == 0)
            {
                await _pluginBindingRepository.DeleteByAgentIdAsync(tenantId, id, cancellationToken);
                if (pluginBindings.Length > 0)
                {
                    await _pluginBindingRepository.AddRangeAsync(pluginBindings, cancellationToken);
                }

                return;
            }

            await _linkRepository.AddRangeAsync(knowledgeBindings, cancellationToken);

            await _pluginBindingRepository.DeleteByAgentIdAsync(tenantId, id, cancellationToken);
            if (pluginBindings.Length > 0)
            {
                await _pluginBindingRepository.AddRangeAsync(pluginBindings, cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task<WorkflowBindingDto> BindWorkflowAsync(
        TenantId tenantId,
        long id,
        long? workflowId,
        CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);
        var workflowBinding = await ResolveWorkflowBindingAsync(tenantId, workflowId, null, cancellationToken);
        entity.Update(
            entity.Name,
            entity.Description,
            entity.AvatarUrl,
            entity.SystemPrompt,
            entity.PersonaMarkdown,
            entity.Goals,
            entity.ReplyLogic,
            entity.OutputFormat,
            entity.Constraints,
            entity.OpeningMessage,
            entity.PresetQuestionsJson,
            entity.DatabaseBindingsJson,
            entity.VariableBindingsJson,
            entity.ModelConfigId,
            entity.ModelName,
            entity.Temperature,
            entity.MaxTokens,
            workflowBinding.WorkflowId,
            workflowBinding.WorkflowName,
            entity.EnableMemory,
            entity.EnableShortTermMemory,
            entity.EnableLongTermMemory,
            entity.LongTermMemoryTopK);

        await _agentRepository.UpdateAsync(entity, cancellationToken);
        return workflowBinding;
    }

    private static string SerializePresetQuestions(IReadOnlyList<string>? presetQuestions)
    {
        var normalized = presetQuestions?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToArray() ?? [];
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static string SerializeIdCollection(IReadOnlyList<long>? ids)
    {
        var normalized = ids?
            .Where(item => item > 0)
            .Distinct()
            .ToArray() ?? [];
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static string SerializeDatabaseBindings(IReadOnlyList<AgentDatabaseBindingInput>? items)
    {
        var normalized = items?
            .Where(item => item.DatabaseId > 0)
            .Select(item => new AgentDatabaseBindingItem(
                item.DatabaseId,
                string.IsNullOrWhiteSpace(item.Alias) ? null : item.Alias.Trim(),
                NormalizeDatabaseAccessMode(item.AccessMode),
                NormalizeTableAllowlist(item.TableAllowlist),
                item.IsDefault))
            .ToArray() ?? [];
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static string SerializeVariableBindings(IReadOnlyList<AgentVariableBindingInput>? items)
    {
        var normalized = items?
            .Where(item => item.VariableId > 0)
            .Select(item => new AgentVariableBindingItem(
                item.VariableId,
                string.IsNullOrWhiteSpace(item.Alias) ? null : item.Alias.Trim(),
                item.IsRequired,
                string.IsNullOrWhiteSpace(item.DefaultValueOverride) ? null : item.DefaultValueOverride.Trim()))
            .ToArray() ?? [];
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _linkRepository.DeleteByAgentIdAsync(tenantId, entity.Id, cancellationToken);
            await _publicationRepository.DeleteByAgentIdAsync(tenantId, entity.Id, cancellationToken);
            await _agentRepository.DeleteAsync(tenantId, entity.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task<long> DuplicateAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken)
    {
        var source = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        var links = await _linkRepository.GetByAgentIdAsync(tenantId, id, cancellationToken);
        var pluginBindings = await _pluginBindingRepository.GetByAgentIdAsync(tenantId, id, cancellationToken);
        var duplicateId = _idGeneratorAccessor.NextId();
        var duplicate = source.CreateDuplicate(duplicateId, $"Copy of {source.Name}", creatorId);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _agentRepository.AddAsync(duplicate, cancellationToken);
            if (links.Count > 0)
            {
                var clonedLinks = links
                    .Select(link => new AgentKnowledgeLink(
                        tenantId,
                        duplicateId,
                        link.KnowledgeBaseId,
                        link.IsEnabled,
                        link.InvokeMode,
                        link.TopK,
                        link.ScoreThreshold,
                        link.EnabledContentTypesJson,
                        link.RewriteQueryTemplate,
                        _idGeneratorAccessor.NextId()))
                    .ToArray();
                await _linkRepository.AddRangeAsync(clonedLinks, cancellationToken);
            }

            if (pluginBindings.Count > 0)
            {
                var clonedBindings = pluginBindings
                    .Select(binding => new AgentPluginBinding(
                        tenantId,
                        duplicateId,
                        binding.PluginId,
                        binding.SortOrder,
                        binding.IsEnabled,
                        binding.ToolConfigJson,
                        _idGeneratorAccessor.NextId()))
                    .ToArray();
                await _pluginBindingRepository.AddRangeAsync(clonedBindings, cancellationToken);
            }
        }, cancellationToken);

        return duplicateId;
    }

    public async Task PublishAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _agentRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("AgentNotFound", ErrorCodes.NotFound);

        entity.Publish();
        await _agentRepository.UpdateAsync(entity, cancellationToken);
    }

    private async Task<long?> ResolveModelConfigIdAsync(
        TenantId tenantId,
        long? requestedModelConfigId,
        CancellationToken cancellationToken)
    {
        if (requestedModelConfigId.HasValue)
        {
            var exists = await _modelConfigRepository.FindByIdAsync(tenantId, requestedModelConfigId.Value, cancellationToken);
            if (exists is null)
            {
                throw new BusinessException("ModelConfigNotFound", ErrorCodes.ValidationError);
            }

            return requestedModelConfigId.Value;
        }

        var enabled = await _modelConfigRepository.GetAllEnabledAsync(tenantId, cancellationToken);
        return enabled.FirstOrDefault()?.Id;
    }

    private async Task<WorkflowBindingDto> ResolveWorkflowBindingAsync(
        TenantId tenantId,
        long? workflowId,
        string? workflowName,
        CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue || workflowId.Value <= 0)
        {
            return new WorkflowBindingDto(null, null);
        }

        var workflow = await _workflowMetaRepository.FindActiveByIdAsync(tenantId, workflowId.Value, cancellationToken)
            ?? throw new BusinessException("WorkflowNotFound", ErrorCodes.ValidationError);
        return new WorkflowBindingDto(
            workflow.Id,
            string.IsNullOrWhiteSpace(workflowName) ? workflow.Name : workflowName.Trim());
    }

    private async Task<AgentPluginBinding[]> BuildValidatedPluginBindingsAsync(
        TenantId tenantId,
        long agentId,
        IReadOnlyList<AgentPluginBindingInput>? inputs,
        CancellationToken cancellationToken)
    {
        var normalizedInputs = inputs?
            .Where(x => x.PluginId > 0)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.PluginId)
            .GroupBy(x => x.PluginId)
            .Select(g => g.First())
            .ToArray() ?? [];

        if (normalizedInputs.Length == 0)
        {
            return [];
        }

        var pluginIds = normalizedInputs
            .Select(x => x.PluginId)
            .Distinct()
            .ToArray();
        var existingPlugins = await _pluginRepository.QueryByIdsAsync(tenantId, pluginIds, cancellationToken);
        if (existingPlugins.Count != pluginIds.Length)
        {
            throw new BusinessException("存在无效的插件绑定。", ErrorCodes.ValidationError);
        }

        var pluginApis = await _pluginApiRepository.GetByPluginIdsAsync(tenantId, pluginIds, cancellationToken);
        var apiIdsByPluginId = pluginApis
            .GroupBy(item => item.PluginId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToHashSet());

        return normalizedInputs
            .Select(binding => new AgentPluginBinding(
                tenantId,
                agentId,
                binding.PluginId,
                binding.SortOrder,
                binding.IsEnabled,
                BuildPluginToolConfigJson(binding, apiIdsByPluginId.GetValueOrDefault(binding.PluginId)),
                _idGeneratorAccessor.NextId()))
            .ToArray();
    }

    private async Task<AgentKnowledgeLink[]> BuildValidatedKnowledgeBindingsAsync(
        TenantId tenantId,
        long agentId,
        IReadOnlyList<AgentKnowledgeBindingInput>? inputs,
        IReadOnlyList<long>? compatibilityIds,
        CancellationToken cancellationToken)
    {
        var normalizedInputs = inputs is { Count: > 0 }
            ? inputs
                .Where(item => item.KnowledgeBaseId > 0)
                .GroupBy(item => item.KnowledgeBaseId)
                .Select(group => group.First())
                .ToArray()
            : compatibilityIds?
                .Where(item => item > 0)
                .Distinct()
                .Select(id => new AgentKnowledgeBindingInput(id, true, "auto", 5, null, ["text", "table", "image"], null))
                .ToArray() ?? [];

        if (normalizedInputs.Length == 0)
        {
            return [];
        }

        var knowledgeBaseIds = normalizedInputs
            .Select(item => item.KnowledgeBaseId)
            .ToArray();
        var knowledgeBases = await _knowledgeBaseRepository.QueryByIdsAsync(tenantId, knowledgeBaseIds, cancellationToken);
        if (knowledgeBases.Count != knowledgeBaseIds.Length)
        {
            throw new BusinessException("存在无效的知识库绑定。", ErrorCodes.ValidationError);
        }

        return normalizedInputs
            .Select(binding => new AgentKnowledgeLink(
                tenantId,
                agentId,
                binding.KnowledgeBaseId,
                binding.IsEnabled,
                binding.InvokeMode,
                binding.TopK,
                binding.ScoreThreshold,
                SerializeStringCollection(binding.EnabledContentTypes),
                binding.RewriteQueryTemplate?.Trim(),
                _idGeneratorAccessor.NextId()))
            .ToArray();
    }

    private async Task<AgentDatabaseBindingInput[]> BuildValidatedDatabaseBindingsAsync(
        TenantId tenantId,
        long botId,
        IReadOnlyList<AgentDatabaseBindingInput>? databaseBindings,
        IReadOnlyList<long>? compatibilityIds,
        CancellationToken cancellationToken)
    {
        var normalizedInputs = databaseBindings is { Count: > 0 }
            ? databaseBindings
                .Where(item => item.DatabaseId > 0)
                .GroupBy(item => item.DatabaseId)
                .Select(group => group.First())
                .Select(item => new AgentDatabaseBindingInput(
                    item.DatabaseId,
                    string.IsNullOrWhiteSpace(item.Alias) ? null : item.Alias.Trim(),
                    NormalizeDatabaseAccessMode(item.AccessMode),
                    NormalizeTableAllowlist(item.TableAllowlist),
                    item.IsDefault))
                .ToArray()
            : (compatibilityIds ?? [])
                .Where(item => item > 0)
                .Distinct()
                .Select((id, index) => new AgentDatabaseBindingInput(id, null, "readonly", Array.Empty<string>(), index == 0))
                .ToArray();

        if (normalizedInputs.Length == 0)
        {
            return [];
        }

        if (normalizedInputs.Count(item => item.IsDefault) > 1)
        {
            throw new BusinessException("只能设置一个默认数据库。", ErrorCodes.ValidationError);
        }

        var normalizedIds = normalizedInputs
            .Select(item => item.DatabaseId)
            .ToArray();
        var databases = await _databaseRepository.QueryByIdsAsync(tenantId, normalizedIds, cancellationToken);
        if (databases.Count != normalizedIds.Length)
        {
            throw new BusinessException("存在无效的数据库绑定。", ErrorCodes.ValidationError);
        }

        var conflict = databases.FirstOrDefault(item => item.BotId.HasValue && item.BotId.Value > 0 && item.BotId.Value != botId);
        if (conflict is not null)
        {
            throw new BusinessException("存在已绑定到其他智能体的数据库。", ErrorCodes.ValidationError);
        }

        return normalizedInputs;
    }

    private async Task<AgentVariableBindingInput[]> BuildValidatedVariableBindingsAsync(
        TenantId tenantId,
        long botId,
        IReadOnlyList<AgentVariableBindingInput>? variableBindings,
        IReadOnlyList<long>? compatibilityIds,
        CancellationToken cancellationToken)
    {
        var normalizedInputs = variableBindings is { Count: > 0 }
            ? variableBindings
                .Where(item => item.VariableId > 0)
                .GroupBy(item => item.VariableId)
                .Select(group => group.First())
                .Select(item => new AgentVariableBindingInput(
                    item.VariableId,
                    string.IsNullOrWhiteSpace(item.Alias) ? null : item.Alias.Trim(),
                    item.IsRequired,
                    string.IsNullOrWhiteSpace(item.DefaultValueOverride) ? null : item.DefaultValueOverride.Trim()))
                .ToArray()
            : (compatibilityIds ?? [])
                .Where(item => item > 0)
                .Distinct()
                .Select(id => new AgentVariableBindingInput(id, null, false, null))
                .ToArray();

        if (normalizedInputs.Length == 0)
        {
            return [];
        }

        var normalizedIds = normalizedInputs
            .Select(item => item.VariableId)
            .ToArray();
        var variables = await _variableRepository.QueryByIdsAsync(tenantId, normalizedIds, cancellationToken);
        if (variables.Count != normalizedIds.Length)
        {
            throw new BusinessException("存在无效的变量绑定。", ErrorCodes.ValidationError);
        }

        var invalidScope = variables.FirstOrDefault(item => item.Scope != AiVariableScope.Bot || item.ScopeId != botId);
        if (invalidScope is not null)
        {
            throw new BusinessException("变量绑定必须属于当前智能体作用域。", ErrorCodes.ValidationError);
        }

        return normalizedInputs;
    }

    private static string BuildPluginToolConfigJson(
        AgentPluginBindingInput binding,
        IReadOnlySet<long>? validApiIds)
    {
        if (binding.ToolBindings is not { Count: > 0 })
        {
            return string.IsNullOrWhiteSpace(binding.ToolConfigJson) ? "{}" : binding.ToolConfigJson!;
        }

        var toolBindings = binding.ToolBindings
            .Where(item => item.ApiId > 0 && (validApiIds is null || validApiIds.Contains(item.ApiId)))
            .Select(item => new
            {
                apiId = item.ApiId,
                isEnabled = item.IsEnabled,
                timeoutSeconds = item.TimeoutSeconds,
                failurePolicy = item.FailurePolicy,
                parameterBindings = (item.ParameterBindings ?? [])
                    .Where(param => !string.IsNullOrWhiteSpace(param.ParameterName))
                    .Select(param => new
                    {
                        parameterName = param.ParameterName.Trim(),
                        valueSource = param.ValueSource,
                        literalValue = param.LiteralValue,
                        variableKey = param.VariableKey
                    })
                    .ToArray()
            })
            .ToArray();

        return JsonSerializer.Serialize(new { toolBindings }, JsonOptions);
    }

    private static string SerializeStringCollection(IReadOnlyList<string>? items)
    {
        var normalized = items?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? ["text", "table", "image"];
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static IReadOnlyList<string> NormalizeTableAllowlist(IReadOnlyList<string>? items)
        => items?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

    private static string NormalizeDatabaseAccessMode(string? value)
        => string.Equals(value, "readwrite", StringComparison.OrdinalIgnoreCase) ? "readwrite" : "readonly";

    private async Task SyncDatabaseBindingsAsync(
        TenantId tenantId,
        long botId,
        IReadOnlyCollection<long> selectedDatabaseIds,
        CancellationToken cancellationToken)
    {
        var (currentItems, _) = await _databaseRepository.GetPagedAsync(tenantId, keyword: null, pageIndex: 1, pageSize: 500, cancellationToken);
        var boundItems = currentItems
            .Where(item => item.BotId.HasValue && item.BotId.Value == botId)
            .ToArray();

        foreach (var database in boundItems)
        {
            if (!selectedDatabaseIds.Contains(database.Id))
            {
                database.UnbindBot();
                await _databaseRepository.UpdateAsync(database, cancellationToken);
            }
        }

        if (selectedDatabaseIds.Count == 0)
        {
            return;
        }

        var selectedEntities = await _databaseRepository.QueryByIdsAsync(tenantId, selectedDatabaseIds.ToArray(), cancellationToken);
        foreach (var database in selectedEntities)
        {
            if (database.BotId != botId)
            {
                database.BindBot(botId);
                await _databaseRepository.UpdateAsync(database, cancellationToken);
            }
        }
    }
}
