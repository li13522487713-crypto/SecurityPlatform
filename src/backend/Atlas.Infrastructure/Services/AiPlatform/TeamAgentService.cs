using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class TeamAgentService : ITeamAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<long, CancellationTokenSource> ConversationCancellationMap = new();

    private readonly TeamAgentRepository _teamAgentRepository;
    private readonly TeamAgentConversationRepository _conversationRepository;
    private readonly TeamAgentMessageRepository _messageRepository;
    private readonly TeamAgentExecutionRepository _executionRepository;
    private readonly TeamAgentSchemaDraftRepository _schemaDraftRepository;
    private readonly AgentRepository _agentRepository;
    private readonly ITeamAgentOrchestrationRuntime _orchestrationRuntime;
    private readonly ITeamAgentSchemaDraftComposer _schemaDraftComposer;
    private readonly IDynamicTableCommandService _dynamicTableCommandService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public TeamAgentService(
        TeamAgentRepository teamAgentRepository,
        TeamAgentConversationRepository conversationRepository,
        TeamAgentMessageRepository messageRepository,
        TeamAgentExecutionRepository executionRepository,
        TeamAgentSchemaDraftRepository schemaDraftRepository,
        AgentRepository agentRepository,
        ITeamAgentOrchestrationRuntime orchestrationRuntime,
        ITeamAgentSchemaDraftComposer schemaDraftComposer,
        IDynamicTableCommandService dynamicTableCommandService,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _teamAgentRepository = teamAgentRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _executionRepository = executionRepository;
        _schemaDraftRepository = schemaDraftRepository;
        _agentRepository = agentRepository;
        _orchestrationRuntime = orchestrationRuntime;
        _schemaDraftComposer = schemaDraftComposer;
        _dynamicTableCommandService = dynamicTableCommandService;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<TeamAgentListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _teamAgentRepository.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        var results = items.Select(item =>
        {
            var members = DeserializeMembers(item.MembersJson);
            return new TeamAgentListItem(
                item.Id,
                "team",
                item.Name,
                item.Description,
                item.TeamMode,
                item.Status,
                DeserializeStringList(item.CapabilityTagsJson),
                members.Count,
                item.DefaultEntrySkill,
                item.PublishVersion,
                DeserializeStringList(item.BoundDataAssetsJson),
                null,
                item.CreatedAt,
                item.UpdatedAt);
        }).ToList();

        return new PagedResult<TeamAgentListItem>(results, total, pageIndex, pageSize);
    }

    public async Task<TeamAgentDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _teamAgentRepository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<long> CreateAsync(TenantId tenantId, long creatorUserId, TeamAgentCreateRequest request, CancellationToken cancellationToken)
    {
        var members = NormalizeMembers(request.Members);
        await EnsureAgentsExistAsync(tenantId, members, cancellationToken);
        var entity = new TeamAgent(
            tenantId,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.TeamMode,
            SerializeStringList(request.CapabilityTags),
            request.DefaultEntrySkill?.Trim(),
            SerializeStringList(request.BoundDataAssets),
            SerializeMembers(members),
            request.SchemaConfigJson ?? "{}",
            creatorUserId,
            _idGeneratorAccessor.NextId());
        await _teamAgentRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, TeamAgentUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await RequireTeamAgentAsync(tenantId, id, cancellationToken);
        var members = NormalizeMembers(request.Members);
        await EnsureAgentsExistAsync(tenantId, members, cancellationToken);
        entity.Update(
            request.Name.Trim(),
            request.Description?.Trim(),
            request.TeamMode,
            request.Status,
            SerializeStringList(request.CapabilityTags),
            request.DefaultEntrySkill?.Trim(),
            SerializeStringList(request.BoundDataAssets),
            SerializeMembers(members),
            request.SchemaConfigJson ?? "{}");
        await _teamAgentRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        if (await _teamAgentRepository.FindByIdAsync(tenantId, id, cancellationToken) is null)
        {
            return;
        }

        await _teamAgentRepository.DeleteAsync(tenantId, id, cancellationToken);
    }

    public async Task<long> DuplicateAsync(TenantId tenantId, long creatorUserId, long id, CancellationToken cancellationToken)
    {
        var source = await RequireTeamAgentAsync(tenantId, id, cancellationToken);
        var duplicated = new TeamAgent(
            tenantId,
            $"{source.Name} Copy",
            source.Description,
            source.TeamMode,
            source.CapabilityTagsJson,
            source.DefaultEntrySkill,
            source.BoundDataAssetsJson,
            source.MembersJson,
            source.SchemaConfigJson,
            creatorUserId,
            _idGeneratorAccessor.NextId());
        await _teamAgentRepository.AddAsync(duplicated, cancellationToken);
        return duplicated.Id;
    }

    public async Task PublishAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await RequireTeamAgentAsync(tenantId, id, cancellationToken);
        entity.Publish();
        await _teamAgentRepository.UpdateAsync(entity, cancellationToken);
    }

    public Task<IReadOnlyList<TeamAgentTemplateItem>> GetTemplatesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TeamAgentTemplateItem>>(BuildTemplates());

    public async Task<long> CreateFromTemplateAsync(
        TenantId tenantId,
        long creatorUserId,
        TeamAgentCreateFromTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var template = BuildTemplates().FirstOrDefault(x => string.Equals(x.Key, request.TemplateKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessException("团队模板不存在。", ErrorCodes.NotFound);
        var createRequest = new TeamAgentCreateRequest(
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? template.Description : request.Description.Trim(),
            template.TeamMode,
            template.CapabilityTags,
            template.DefaultEntrySkill,
            [],
            template.Members.Select(member => new TeamAgentMemberInput(
                member.AgentId,
                member.RoleName,
                member.Responsibility,
                member.Alias,
                member.SortOrder,
                member.IsEnabled,
                member.PromptPrefix,
                member.CapabilityTags)).ToList(),
            "{}");
        return await CreateAsync(tenantId, creatorUserId, createRequest, cancellationToken);
    }

    public async Task<long> CreateConversationAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        TeamAgentConversationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var teamAgent = await RequireTeamAgentAsync(tenantId, teamAgentId, cancellationToken);
        var title = string.IsNullOrWhiteSpace(request.Title) ? $"与 {teamAgent.Name} 的团队会话" : request.Title.Trim();
        var entity = new TeamAgentConversation(tenantId, teamAgentId, userId, title, _idGeneratorAccessor.NextId());
        await _conversationRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task<PagedResult<TeamAgentConversationDto>> ListConversationsAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _conversationRepository.GetPagedByTeamAgentAsync(tenantId, teamAgentId, userId, pageIndex, pageSize, cancellationToken);
        return new PagedResult<TeamAgentConversationDto>(items.Select(MapConversation).ToList(), total, pageIndex, pageSize);
    }

    public async Task<TeamAgentConversationDto?> GetConversationAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken)
    {
        var entity = await _conversationRepository.FindByIdAsync(tenantId, conversationId, cancellationToken);
        if (entity is not null && entity.UserId != userId)
        {
            throw new BusinessException("无权访问团队会话。", ErrorCodes.Forbidden);
        }

        return entity is null ? null : MapConversation(entity);
    }

    public async Task UpdateConversationAsync(TenantId tenantId, long userId, long conversationId, TeamAgentConversationUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        entity.UpdateTitle(request.Title.Trim());
        await _conversationRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteConversationAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken)
    {
        _ = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _messageRepository.DeleteByConversationAsync(tenantId, conversationId, cancellationToken);
            await _conversationRepository.DeleteAsync(tenantId, conversationId, cancellationToken);
        }, cancellationToken);
    }

    public async Task ClearConversationContextAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken)
    {
        var entity = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        entity.ClearContext(DateTime.UtcNow);
        var marker = new TeamAgentMessage(
            tenantId,
            conversationId,
            "system",
            "[CONTEXT_CLEARED]",
            null,
            "context.cleared",
            null,
            true,
            _idGeneratorAccessor.NextId());

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _messageRepository.AddAsync(marker, cancellationToken);
            await _conversationRepository.UpdateAsync(entity, cancellationToken);
        }, cancellationToken);
    }

    public async Task ClearConversationHistoryAsync(TenantId tenantId, long userId, long conversationId, CancellationToken cancellationToken)
    {
        var entity = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        entity.ResetMessages();
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _messageRepository.DeleteByConversationAsync(tenantId, conversationId, cancellationToken);
            await _conversationRepository.UpdateAsync(entity, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<TeamAgentMessageDto>> GetConversationMessagesAsync(
        TenantId tenantId,
        long userId,
        long conversationId,
        bool includeContextMarkers,
        int? limit,
        CancellationToken cancellationToken)
    {
        _ = await RequireConversationAsync(tenantId, userId, conversationId, cancellationToken);
        var items = await _messageRepository.GetAllByConversationAsync(tenantId, conversationId, cancellationToken);
        var filtered = includeContextMarkers ? items : items.Where(x => !x.IsContextCleared).ToList();
        if (limit.HasValue && limit.Value > 0 && filtered.Count > limit.Value)
        {
            filtered = filtered.Skip(filtered.Count - limit.Value).ToList();
        }

        return filtered.Select(MapMessage).ToList();
    }

    public async Task<TeamAgentChatResponse> ChatAsync(
        TenantId tenantId,
        long userId,
        long teamAgentId,
        TeamAgentChatRequest request,
        string? appId,
        CancellationToken cancellationToken)
        => await ExecuteChatAsync(tenantId, userId, teamAgentId, request, appId, null, cancellationToken);

    public async IAsyncEnumerable<TeamAgentRunEvent> ChatStreamAsync(
        TenantId tenantId,
        long userId,
        long teamAgentId,
        TeamAgentChatRequest request,
        string? appId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<TeamAgentRunEvent>();
        var producer = Task.Run(async () =>
        {
            try
            {
                await ExecuteChatAsync(
                    tenantId,
                    userId,
                    teamAgentId,
                    request,
                    appId,
                    async evt => await channel.Writer.WriteAsync(evt, cancellationToken),
                    cancellationToken);
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, cancellationToken);

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        await producer;
    }

    public async Task CancelChatAsync(TenantId tenantId, long userId, long teamAgentId, TeamAgentChatCancelRequest request, CancellationToken cancellationToken)
    {
        var conversation = await RequireConversationAsync(tenantId, userId, request.ConversationId, cancellationToken);
        if (conversation.TeamAgentId != teamAgentId)
        {
            throw new BusinessException("团队会话与 Team Agent 不匹配。", ErrorCodes.ValidationError);
        }

        if (ConversationCancellationMap.TryRemove(request.ConversationId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public async Task<TeamAgentExecutionResult?> GetExecutionAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executionRepository.FindByIdAsync(tenantId, executionId, cancellationToken);
        return execution is null ? null : MapExecution(execution);
    }

    public async Task<long> CreateSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long userId,
        SchemaDraftCreateRequest request,
        string? appId,
        CancellationToken cancellationToken)
    {
        var teamAgent = await RequireTeamAgentAsync(tenantId, teamAgentId, cancellationToken);
        var draft = _schemaDraftComposer.Compose(teamAgent, request.Requirement, [], appId);
        var entity = new TeamAgentSchemaDraft(
            tenantId,
            teamAgentId,
            request.ConversationId,
            userId,
            string.IsNullOrWhiteSpace(request.Title) ? $"{teamAgent.Name} 草案" : request.Title.Trim(),
            request.Requirement.Trim(),
            JsonSerializer.Serialize(draft, JsonOptions),
            JsonSerializer.Serialize(draft.OpenQuestions, JsonOptions),
            appId ?? string.Empty,
            _idGeneratorAccessor.NextId());
        await _schemaDraftRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task<TeamAgentSchemaDraftDetail?> GetSchemaDraftAsync(TenantId tenantId, long teamAgentId, long draftId, CancellationToken cancellationToken)
    {
        var entity = await _schemaDraftRepository.FindByTeamAgentAndIdAsync(tenantId, teamAgentId, draftId, cancellationToken);
        return entity is null ? null : MapDraft(entity);
    }

    public async Task UpdateSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        long userId,
        SchemaDraftUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _ = userId;
        var entity = await RequireDraftAsync(tenantId, teamAgentId, draftId, cancellationToken);
        entity.UpdateDraft(
            request.Title.Trim(),
            request.Requirement.Trim(),
            JsonSerializer.Serialize(request.SchemaDraft, JsonOptions),
            JsonSerializer.Serialize(request.OpenQuestions ?? [], JsonOptions));
        await _schemaDraftRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<SchemaDraftConfirmationResponse> ConfirmSchemaDraftAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        long userId,
        SchemaDraftConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
        {
            throw new BusinessException("必须显式确认后才能创建表。", ErrorCodes.ValidationError);
        }

        var entity = await RequireDraftAsync(tenantId, teamAgentId, draftId, cancellationToken);
        var draft = DeserializeDraft(entity.DraftJson);
        if (draft.OpenQuestions.Count > 0)
        {
            throw new BusinessException("草案仍存在待确认问题，不能直接创建。", ErrorCodes.ValidationError);
        }

        var resources = new List<SchemaDraftCreatedResourceDto>();
        foreach (var table in draft.Entities)
        {
            var fields = draft.Fields.Where(x => string.Equals(x.TableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.SortOrder)
                .Select(MapField)
                .ToList();
            var indexes = draft.Indexes.Where(x => string.Equals(x.TableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                .Select(index => new DynamicIndexDefinition(index.Name, index.IsUnique, index.Fields))
                .ToList();
            var createRequest = new DynamicTableCreateRequest(table.TableKey, table.DisplayName, table.Description, "Sqlite", fields, indexes, entity.AppId);
            var createdId = await _dynamicTableCommandService.CreateAsync(tenantId, userId, createRequest, cancellationToken);
            resources.Add(new SchemaDraftCreatedResourceDto(table.TableKey, createdId.ToString()));
        }

        foreach (var table in draft.Entities)
        {
            var tableRelations = draft.Relations.Where(x => string.Equals(x.SourceTableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                .Select(relation => new DynamicRelationDefinition(relation.RelatedTableKey, relation.SourceField, relation.TargetField, relation.RelationType, relation.CascadeRule))
                .ToList();
            if (tableRelations.Count > 0)
            {
                await _dynamicTableCommandService.SetRelationsAsync(tenantId, userId, table.TableKey, new DynamicRelationUpsertRequest(tableRelations), cancellationToken);
            }

            var permissions = draft.SecurityPolicies.Where(x => string.Equals(x.TableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                .Select(policy => new DynamicFieldPermissionRule(policy.FieldName, policy.RoleCode, policy.CanView, policy.CanEdit))
                .ToList();
            if (permissions.Count > 0)
            {
                await _dynamicTableCommandService.SetFieldPermissionsAsync(tenantId, userId, table.TableKey, new DynamicFieldPermissionUpsertRequest(permissions), cancellationToken);
            }
        }

        entity.Confirm(JsonSerializer.Serialize(resources.Select(x => x.TableKey).ToList(), JsonOptions));
        await _schemaDraftRepository.UpdateAsync(entity, cancellationToken);
        return new SchemaDraftConfirmationResponse(entity.Id, entity.ConfirmationState.ToString().ToLowerInvariant(), resources.Select(x => x.TableKey).ToList(), resources);
    }

    public async Task DiscardSchemaDraftAsync(TenantId tenantId, long teamAgentId, long draftId, CancellationToken cancellationToken)
    {
        var entity = await RequireDraftAsync(tenantId, teamAgentId, draftId, cancellationToken);
        entity.Discard();
        await _schemaDraftRepository.UpdateAsync(entity, cancellationToken);
    }

    private async Task<TeamAgentChatResponse> ExecuteChatAsync(
        TenantId tenantId,
        long userId,
        long teamAgentId,
        TeamAgentChatRequest request,
        string? appId,
        Func<TeamAgentRunEvent, Task>? emitAsync,
        CancellationToken cancellationToken)
    {
        var teamAgent = await RequireTeamAgentAsync(tenantId, teamAgentId, cancellationToken);
        var conversation = await EnsureConversationAsync(tenantId, userId, teamAgent, request, cancellationToken);
        var execution = new TeamAgentExecution(tenantId, teamAgentId, conversation.Id, userId, request.Message.Trim(), _idGeneratorAccessor.NextId());
        await _executionRepository.AddAsync(execution, cancellationToken);
        execution.MarkRunning();
        await _executionRepository.UpdateAsync(execution, cancellationToken);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConversationCancellationMap[conversation.Id] = linkedCts;

        var events = new List<TeamAgentRunEvent>();
        var steps = new List<TeamAgentExecutionStep>();
        SchemaDraftDto? draft = null;

        try
        {
            var userMessage = new TeamAgentMessage(
                tenantId,
                conversation.Id,
                "user",
                request.Message.Trim(),
                null,
                "conversation.started",
                null,
                false,
                _idGeneratorAccessor.NextId());
            conversation.AddMessage(userMessage.CreatedAt);
            await PersistMessageAsync(conversation, userMessage, linkedCts.Token);

            await AddEventAsync(events, emitAsync, new TeamAgentRunEvent("conversation.started", JsonSerializer.Serialize(new
            {
                conversationId = conversation.Id,
                executionId = execution.Id,
                teamAgentId
            })), linkedCts.Token);

            var members = DeserializeMembers(teamAgent.MembersJson).Where(x => x.IsEnabled).OrderBy(x => x.SortOrder).ToList();
            await EnsureAgentsExistAsync(tenantId, members, linkedCts.Token);
            TeamAgentOrchestrationRuntimeResult runtimeResult;
            try
            {
                runtimeResult = await _orchestrationRuntime.ExecuteAsync(
                    new TeamAgentOrchestrationRuntimeRequest(
                        tenantId,
                        userId,
                        teamAgent,
                        members,
                        request.Message.Trim(),
                        request.EnableRag,
                        appId),
                    evt => AddEventAsync(events, emitAsync, evt, linkedCts.Token),
                    async contribution =>
                    {
                        var message = new TeamAgentMessage(
                            tenantId,
                            conversation.Id,
                            "assistant",
                            contribution.OutputMessage,
                            JsonSerializer.Serialize(new
                            {
                                agentId = contribution.AgentId,
                                agentName = contribution.AgentName,
                                roleName = contribution.RoleName
                            }, JsonOptions),
                            "member.message",
                            contribution.RoleName,
                            false,
                            _idGeneratorAccessor.NextId());
                        conversation.AddMessage(message.CreatedAt);
                        await PersistMessageAsync(conversation, message, linkedCts.Token);
                    },
                    linkedCts.Token);
            }
            catch (TeamAgentOrchestrationExecutionException ex)
            {
                foreach (var step in ex.Steps)
                {
                    steps.Add(step);
                }

                await AddEventAsync(events, emitAsync, new TeamAgentRunEvent("conversation.failed", JsonSerializer.Serialize(new
                {
                    executionId = execution.Id,
                    error = ex.Message
                })), linkedCts.Token);
                execution.Fail(ex.Message, JsonSerializer.Serialize(steps, JsonOptions), JsonSerializer.Serialize(events, JsonOptions));
                await _executionRepository.UpdateAsync(execution, linkedCts.Token);
                throw ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                await AddEventAsync(events, emitAsync, new TeamAgentRunEvent("conversation.failed", JsonSerializer.Serialize(new
                {
                    executionId = execution.Id,
                    error = ex.Message
                })), linkedCts.Token);
                execution.Fail(ex.Message, JsonSerializer.Serialize(steps, JsonOptions), JsonSerializer.Serialize(events, JsonOptions));
                await _executionRepository.UpdateAsync(execution, linkedCts.Token);
                throw;
            }

            foreach (var step in runtimeResult.Steps)
            {
                steps.Add(step);
            }

            var currentMessage = runtimeResult.FinalMessage;

            if (request.GenerateSchemaDraft == true || string.Equals(teamAgent.DefaultEntrySkill, "schema_builder", StringComparison.OrdinalIgnoreCase))
            {
                draft = _schemaDraftComposer.Compose(teamAgent, request.Message, runtimeResult.Contributions, appId);
                await AddEventAsync(events, emitAsync, new TeamAgentRunEvent("schema.draft.updated", JsonSerializer.Serialize(draft, JsonOptions)), linkedCts.Token);
            }

            execution.Complete(currentMessage, JsonSerializer.Serialize(steps, JsonOptions), JsonSerializer.Serialize(events, JsonOptions));
            await _executionRepository.UpdateAsync(execution, linkedCts.Token);
            await AddEventAsync(events, emitAsync, new TeamAgentRunEvent("conversation.completed", JsonSerializer.Serialize(new
            {
                executionId = execution.Id,
                conversationId = conversation.Id,
                content = currentMessage
            })), linkedCts.Token);

            return new TeamAgentChatResponse(conversation.Id, execution.Id, currentMessage, events, draft);
        }
        finally
        {
            ConversationCancellationMap.TryRemove(conversation.Id, out _);
        }
    }

    private async Task AddEventAsync(
        List<TeamAgentRunEvent> events,
        Func<TeamAgentRunEvent, Task>? emitAsync,
        TeamAgentRunEvent evt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        events.Add(evt);
        if (emitAsync is not null)
        {
            await emitAsync(evt);
        }
    }

    private async Task PersistMessageAsync(TeamAgentConversation conversation, TeamAgentMessage message, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _messageRepository.AddAsync(message, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }, cancellationToken);
    }

    private async Task<TeamAgentConversation> EnsureConversationAsync(
        TenantId tenantId,
        long userId,
        TeamAgent teamAgent,
        TeamAgentChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConversationId.HasValue)
        {
            var existing = await RequireConversationAsync(tenantId, userId, request.ConversationId.Value, cancellationToken);
            if (existing.TeamAgentId != teamAgent.Id)
            {
                throw new BusinessException("团队会话与 Team Agent 不匹配。", ErrorCodes.ValidationError);
            }

            return existing;
        }

        var conversation = new TeamAgentConversation(
            tenantId,
            teamAgent.Id,
            userId,
            $"与 {teamAgent.Name} 的团队会话",
            _idGeneratorAccessor.NextId());
        await _conversationRepository.AddAsync(conversation, cancellationToken);
        return conversation;
    }

    private async Task<TeamAgent> RequireTeamAgentAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => await _teamAgentRepository.FindByIdAsync(tenantId, id, cancellationToken)
           ?? throw new BusinessException("Team Agent 不存在。", ErrorCodes.NotFound);

    private async Task<TeamAgentConversation> RequireConversationAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("团队会话不存在。", ErrorCodes.NotFound);
        if (conversation.UserId != userId)
        {
            throw new BusinessException("无权访问团队会话。", ErrorCodes.Forbidden);
        }

        return conversation;
    }

    private async Task<TeamAgentSchemaDraft> RequireDraftAsync(TenantId tenantId, long teamAgentId, long draftId, CancellationToken cancellationToken)
        => await _schemaDraftRepository.FindByTeamAgentAndIdAsync(tenantId, teamAgentId, draftId, cancellationToken)
           ?? throw new BusinessException("SchemaDraft 不存在。", ErrorCodes.NotFound);

    private async Task EnsureAgentsExistAsync(
        TenantId tenantId,
        IReadOnlyList<TeamAgentMemberItem> members,
        CancellationToken cancellationToken)
    {
        var ids = members.Select(x => x.AgentId).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var agents = await _agentRepository.QueryByIdsAsync(tenantId, ids, cancellationToken);
        var missing = ids.Except(agents.Select(x => x.Id)).FirstOrDefault();
        if (missing > 0)
        {
            throw new BusinessException($"团队成员 Agent 不存在: {missing}", ErrorCodes.ValidationError);
        }
    }

    private static TeamAgentDetail MapDetail(TeamAgent entity)
        => new(
            entity.Id,
            "team",
            entity.Name,
            entity.Description,
            entity.TeamMode,
            entity.Status,
            DeserializeStringList(entity.CapabilityTagsJson),
            entity.DefaultEntrySkill,
            entity.PublishVersion,
            DeserializeStringList(entity.BoundDataAssetsJson),
            entity.SchemaConfigJson,
            entity.CreatorUserId,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.PublishedAt,
            DeserializeMembers(entity.MembersJson));

    private static TeamAgentConversationDto MapConversation(TeamAgentConversation entity)
        => new(
            entity.Id,
            entity.TeamAgentId,
            entity.UserId,
            entity.Title,
            entity.CreatedAt,
            entity.LastMessageAt > DateTime.UnixEpoch ? entity.LastMessageAt : null,
            entity.MessageCount);

    private static TeamAgentMessageDto MapMessage(TeamAgentMessage entity)
        => new(
            entity.Id,
            entity.Role,
            entity.Content,
            entity.EventType,
            entity.MemberName,
            entity.Metadata,
            entity.CreatedAt,
            entity.IsContextCleared);

    private static TeamAgentExecutionResult MapExecution(TeamAgentExecution entity)
        => new(
            entity.Id,
            entity.TeamAgentId,
            entity.ConversationId,
            entity.Status.ToString().ToLowerInvariant(),
            entity.OutputMessage,
            entity.ErrorMessage,
            DeserializeSteps(entity.TraceJson),
            DeserializeEvents(entity.EventTraceJson),
            entity.StartedAt,
            entity.CompletedAt);

    private static TeamAgentSchemaDraftDetail MapDraft(TeamAgentSchemaDraft entity)
        => new(
            entity.Id,
            entity.TeamAgentId,
            entity.ConversationId,
            entity.Title,
            entity.Requirement,
            DeserializeDraft(entity.DraftJson),
            entity.Status.ToString().ToLowerInvariant(),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ConfirmedAt);

    private static IReadOnlyList<TeamAgentTemplateItem> BuildTemplates()
        => new List<TeamAgentTemplateItem>
        {
            new(
                "schema_builder",
                "数据建模团队",
                "面向数据管理的建表协作团队",
                TeamAgentMode.GroupChat,
                ["schema_builder", "knowledge"],
                "schema_builder",
                [
                    new TeamAgentMemberItem(0, "业务分析 Agent", "拆解业务实体与字段", "analyst", 1, true, "专注业务建模分析。", ["analysis"]),
                    new TeamAgentMemberItem(0, "DBA Agent", "给出主键、索引与关系建议", "dba", 2, true, "专注数据库设计。", ["schema"]),
                    new TeamAgentMemberItem(0, "权限策略 Agent", "生成字段权限建议", "security", 3, true, "专注权限与隔离设计。", ["security"])
                ]),
            new(
                "document_review",
                "文档审查团队",
                "用于文档审核与风险识别",
                TeamAgentMode.Workflow,
                ["knowledge"],
                "chat",
                [new TeamAgentMemberItem(0, "审查 Agent", "进行文档审查", "reviewer", 1, true, null, ["review"])]),
            new(
                "security_analysis",
                "安全分析团队",
                "用于漏洞分析与汇总",
                TeamAgentMode.Workflow,
                ["ops"],
                "ops",
                [new TeamAgentMemberItem(0, "安全 Agent", "分析安全风险", "security", 1, true, null, ["security"])]),
            new(
                "customer_service",
                "客服协作团队",
                "用于工单分流与回复",
                TeamAgentMode.Handoff,
                ["chat"],
                "chat",
                [new TeamAgentMemberItem(0, "客服 Agent", "处理客户请求", "support", 1, true, null, ["support"])])
        };

    private static DynamicFieldDefinition MapField(SchemaDraftFieldDto field)
        => new(
            field.Name,
            field.DisplayName,
            field.FieldType,
            field.Length,
            field.Precision,
            field.Scale,
            field.AllowNull,
            field.IsPrimaryKey,
            field.IsAutoIncrement,
            field.IsUnique,
            field.DefaultValue,
            field.SortOrder);

    private static List<TeamAgentMemberItem> NormalizeMembers(IReadOnlyList<TeamAgentMemberInput> members)
        => members
            .Where(x => x.AgentId >= 0)
            .Select(x => new TeamAgentMemberItem(
                x.AgentId,
                string.IsNullOrWhiteSpace(x.RoleName) ? $"Agent-{x.AgentId}" : x.RoleName.Trim(),
                x.Responsibility?.Trim(),
                x.Alias?.Trim(),
                x.SortOrder,
                x.IsEnabled,
                x.PromptPrefix?.Trim(),
                x.CapabilityTags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? []))
            .OrderBy(x => x.SortOrder)
            .ToList();

    private static string SerializeMembers(IReadOnlyList<TeamAgentMemberItem> members)
        => JsonSerializer.Serialize(members, JsonOptions);

    private static List<TeamAgentMemberItem> DeserializeMembers(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<TeamAgentMemberItem>>(json, JsonOptions) ?? [];

    private static string SerializeStringList(IReadOnlyList<string>? values)
        => JsonSerializer.Serialize(values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [], JsonOptions);

    private static List<string> DeserializeStringList(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static List<TeamAgentExecutionStep> DeserializeSteps(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<TeamAgentExecutionStep>>(json, JsonOptions) ?? [];

    private static List<TeamAgentRunEvent> DeserializeEvents(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<TeamAgentRunEvent>>(json, JsonOptions) ?? [];

    private static SchemaDraftDto DeserializeDraft(string json)
        => JsonSerializer.Deserialize<SchemaDraftDto>(json, JsonOptions)
           ?? new SchemaDraftDto(string.Empty, [], [], [], [], [], [], TeamAgentSchemaDraftConfirmationState.Pending.ToString().ToLowerInvariant());
}
