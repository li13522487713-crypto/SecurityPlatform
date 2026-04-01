using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
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
    private readonly TeamAgentPublicationRepository _publicationRepository;
    private readonly TeamAgentTemplateRepository _templateRepository;
    private readonly TeamAgentTemplateMemberRepository _templateMemberRepository;
    private readonly TeamAgentConversationRepository _conversationRepository;
    private readonly TeamAgentMessageRepository _messageRepository;
    private readonly TeamAgentExecutionRepository _executionRepository;
    private readonly TeamAgentExecutionStepRepository _executionStepRepository;
    private readonly TeamAgentSchemaDraftRepository _schemaDraftRepository;
    private readonly TeamAgentSchemaDraftExecutionAuditRepository _schemaDraftAuditRepository;
    private readonly MultiAgentOrchestrationRepository _multiAgentRepository;
    private readonly AgentRepository _agentRepository;
    private readonly ITeamAgentOrchestrationRuntime _orchestrationRuntime;
    private readonly ITeamAgentSchemaDraftComposer _schemaDraftComposer;
    private readonly IDynamicTableCommandService _dynamicTableCommandService;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public TeamAgentService(
        TeamAgentRepository teamAgentRepository,
        TeamAgentPublicationRepository publicationRepository,
        TeamAgentTemplateRepository templateRepository,
        TeamAgentTemplateMemberRepository templateMemberRepository,
        TeamAgentConversationRepository conversationRepository,
        TeamAgentMessageRepository messageRepository,
        TeamAgentExecutionRepository executionRepository,
        TeamAgentExecutionStepRepository executionStepRepository,
        TeamAgentSchemaDraftRepository schemaDraftRepository,
        TeamAgentSchemaDraftExecutionAuditRepository schemaDraftAuditRepository,
        MultiAgentOrchestrationRepository multiAgentRepository,
        AgentRepository agentRepository,
        ITeamAgentOrchestrationRuntime orchestrationRuntime,
        ITeamAgentSchemaDraftComposer schemaDraftComposer,
        IDynamicTableCommandService dynamicTableCommandService,
        IAuditRecorder auditRecorder,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _teamAgentRepository = teamAgentRepository;
        _publicationRepository = publicationRepository;
        _templateRepository = templateRepository;
        _templateMemberRepository = templateMemberRepository;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _executionRepository = executionRepository;
        _executionStepRepository = executionStepRepository;
        _schemaDraftRepository = schemaDraftRepository;
        _schemaDraftAuditRepository = schemaDraftAuditRepository;
        _multiAgentRepository = multiAgentRepository;
        _agentRepository = agentRepository;
        _orchestrationRuntime = orchestrationRuntime;
        _schemaDraftComposer = schemaDraftComposer;
        _dynamicTableCommandService = dynamicTableCommandService;
        _auditRecorder = auditRecorder;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<TeamAgentListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        TeamAgentMode? teamMode,
        TeamAgentStatus? status,
        string? capabilityTag,
        string? defaultEntrySkill,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _teamAgentRepository.GetPagedAsync(
            tenantId,
            keyword,
            teamMode,
            status,
            capabilityTag,
            defaultEntrySkill,
            pageIndex,
            pageSize,
            cancellationToken);
        var results = items.Select(MapListItem).ToList();
        return new PagedResult<TeamAgentListItem>(results, total, pageIndex, pageSize);
    }

    public async Task<TeamAgentDashboardDto> GetDashboardAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var teamAgents = await _teamAgentRepository.GetAllAsync(tenantId, cancellationToken);
        var recentExecutions = await _executionRepository.GetRecentAsync(tenantId, 10, cancellationToken);
        var recentConversations = await _conversationRepository.GetRecentAsync(tenantId, 10, cancellationToken);
        var recentDrafts = await _schemaDraftRepository.GetRecentAsync(tenantId, 10, cancellationToken);

        var distinctBoundMembers = teamAgents
            .SelectMany(agent => DeserializeMembers(agent.MembersJson))
            .Where(member => member.IsEnabled && member.AgentId.HasValue && member.AgentId.Value > 0)
            .Select(member => member.AgentId!.Value)
            .Distinct()
            .Count();

        var recentActivities = new List<TeamAgentDashboardActivityItem>();
        var teamAgentMap = teamAgents.ToDictionary(item => item.Id);

        recentActivities.AddRange(recentConversations.Select(item =>
        {
            var team = teamAgentMap.GetValueOrDefault(item.TeamAgentId);
            return new TeamAgentDashboardActivityItem(
                "conversation",
                item.Id,
                item.TeamAgentId,
                team?.Name ?? $"TeamAgent-{item.TeamAgentId}",
                item.Title ?? "团队会话",
                $"{item.MessageCount} 条消息",
                item.LastMessageAt > DateTime.UnixEpoch ? item.LastMessageAt : item.CreatedAt);
        }));
        recentActivities.AddRange(recentExecutions.Select(item =>
        {
            var team = teamAgentMap.GetValueOrDefault(item.TeamAgentId);
            return new TeamAgentDashboardActivityItem(
                "execution",
                item.Id,
                item.TeamAgentId,
                team?.Name ?? $"TeamAgent-{item.TeamAgentId}",
                "团队运行",
                item.Status == TeamAgentExecutionStatus.Failed ? (item.ErrorMessage ?? "执行失败") : (item.OutputMessage ?? "执行完成"),
                item.CompletedAt ?? item.StartedAt);
        }));
        recentActivities.AddRange(recentDrafts.Select(item =>
        {
            var team = teamAgentMap.GetValueOrDefault(item.TeamAgentId);
            return new TeamAgentDashboardActivityItem(
                "schema_draft",
                item.Id,
                item.TeamAgentId,
                team?.Name ?? $"TeamAgent-{item.TeamAgentId}",
                item.Title,
                item.Requirement,
                item.UpdatedAt);
        }));

        return new TeamAgentDashboardDto(
            teamAgents.Count,
            teamAgents.Count,
            distinctBoundMembers,
            await _executionRepository.CountRecentCompletedAsync(tenantId, DateTime.UtcNow.AddDays(-7), cancellationToken),
            await _teamAgentRepository.CountByCapabilityTagAsync(tenantId, "schema_builder", cancellationToken),
            recentActivities.OrderByDescending(item => item.OccurredAt).Take(20).ToList());
    }

    public async Task<TeamAgentDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _teamAgentRepository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<long> CreateAsync(TenantId tenantId, long creatorUserId, TeamAgentCreateRequest request, CancellationToken cancellationToken)
    {
        var members = NormalizeMembers(request.Members);
        ValidateMembers(members);
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
        ValidateMembers(members);
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

    public async Task PublishAsync(
        TenantId tenantId,
        long id,
        long publisherUserId,
        TeamAgentPublicationPublishRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await RequireTeamAgentAsync(tenantId, id, cancellationToken);
        var nextVersion = await _publicationRepository.GetLatestVersionAsync(tenantId, id, cancellationToken) + 1;
        var publication = new TeamAgentPublication(
            tenantId,
            id,
            nextVersion,
            BuildPublicationSnapshot(entity),
            request.ReleaseNote?.Trim(),
            publisherUserId,
            _idGeneratorAccessor.NextId());

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _publicationRepository.DeactivateActiveByTeamAgentIdAsync(tenantId, id, cancellationToken);
            entity.Publish();
            await _teamAgentRepository.UpdateAsync(entity, cancellationToken);
            await _publicationRepository.AddAsync(publication, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<TeamAgentTemplateItem>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        var systemTenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var templates = await _templateRepository.GetAllAsync(systemTenantId, cancellationToken);
        var members = await _templateMemberRepository.GetByTemplateIdsAsync(systemTenantId, templates.Select(item => item.Id).ToList(), cancellationToken);

        return templates.Select(template => new TeamAgentTemplateItem(
            template.Key,
            template.Name,
            template.Description,
            template.TeamMode,
            DeserializeStringList(template.CapabilityTagsJson),
            template.DefaultEntrySkill ?? string.Empty,
            members.Where(item => item.TemplateId == template.Id)
                .OrderBy(item => item.SortOrder)
                .Select(MapTemplateMember)
                .ToList())).ToList();
    }

    public async Task<long> CreateFromTemplateAsync(
        TenantId tenantId,
        long creatorUserId,
        TeamAgentCreateFromTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var systemTenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var template = await _templateRepository.FindByKeyAsync(systemTenantId, request.TemplateKey.Trim(), cancellationToken)
            ?? throw new BusinessException("团队模板不存在。", ErrorCodes.NotFound);
        var templateMembers = await _templateMemberRepository.GetByTemplateIdsAsync(systemTenantId, [template.Id], cancellationToken);
        var bindings = (request.MemberBindings ?? []).ToDictionary(item => item.RoleName, StringComparer.OrdinalIgnoreCase);

        var members = templateMembers
            .OrderBy(item => item.SortOrder)
            .Select(item =>
            {
                var binding = bindings.GetValueOrDefault(item.RoleName);
                var boundAgentId = binding?.AgentId;
                var isEnabled = binding?.IsEnabled ?? (item.IsEnabled && boundAgentId.HasValue && boundAgentId.Value > 0);
                return new TeamAgentMemberInput(
                    boundAgentId,
                    item.RoleName,
                    item.Responsibility,
                    item.Alias,
                    item.SortOrder,
                    isEnabled,
                    item.PromptPrefix,
                    DeserializeStringList(item.CapabilityTagsJson));
            }).ToList();

        return await CreateAsync(
            tenantId,
            creatorUserId,
            new TeamAgentCreateRequest(
                request.Name.Trim(),
                string.IsNullOrWhiteSpace(request.Description) ? template.Description : request.Description.Trim(),
                template.TeamMode,
                DeserializeStringList(template.CapabilityTagsJson),
                template.DefaultEntrySkill,
                [],
                members,
                "{}"),
            cancellationToken);
    }

    public async Task<TeamAgentLegacyMigrationResult> MigrateLegacyAsync(
        TenantId tenantId,
        long creatorUserId,
        TeamAgentLegacyMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var (legacyItems, _) = await _multiAgentRepository.GetPagedAsync(tenantId, null, 1, int.MaxValue, cancellationToken);
        if (request.LegacyIds is { Count: > 0 })
        {
            legacyItems = legacyItems.Where(item => request.LegacyIds.Contains(item.Id)).ToList();
        }

        var createdTeamAgentIds = new List<long>();
        foreach (var legacy in legacyItems)
        {
            var legacySourceId = legacy.Id.ToString();
            if (await _teamAgentRepository.FindByLegacySourceAsync(tenantId, "multi-agent-orchestration", legacySourceId, cancellationToken) is not null)
            {
                continue;
            }

            var members = DeserializeLegacyMembers(legacy.MembersJson)
                .Select((member, index) => new TeamAgentMemberInput(
                    member.AgentId,
                    member.Alias ?? $"Member-{index + 1}",
                    legacy.Mode == MultiAgentOrchestrationMode.Sequential ? "由旧编排迁移的顺序节点" : "由旧编排迁移的并行节点",
                    member.Alias,
                    member.SortOrder,
                    member.IsEnabled,
                    member.PromptPrefix,
                    []))
                .ToList();

            var newId = await CreateAsync(
                tenantId,
                creatorUserId,
                new TeamAgentCreateRequest(
                    legacy.Name,
                    legacy.Description,
                    legacy.Mode == MultiAgentOrchestrationMode.Sequential ? TeamAgentMode.Workflow : TeamAgentMode.GroupChat,
                    [],
                    "chat",
                    [],
                    members,
                    "{}"),
                cancellationToken);

            var created = await RequireTeamAgentAsync(tenantId, newId, cancellationToken);
            created.SetLegacySource("multi-agent-orchestration", legacySourceId);
            await _teamAgentRepository.UpdateAsync(created, cancellationToken);
            createdTeamAgentIds.Add(newId);
        }

        return new TeamAgentLegacyMigrationResult(legacyItems.Count, createdTeamAgentIds.Count, createdTeamAgentIds);
    }

    public async Task<TeamAgentLegacyMigrationStatusDto> GetLegacyMigrationStatusAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var (legacyItems, _) = await _multiAgentRepository.GetPagedAsync(tenantId, null, 1, int.MaxValue, cancellationToken);
        var teamAgents = await _teamAgentRepository.GetAllAsync(tenantId, cancellationToken);
        var migrationMap = teamAgents
            .Where(item => string.Equals(item.LegacySourceType, "multi-agent-orchestration", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.LegacySourceId))
            .ToDictionary(item => item.LegacySourceId!, StringComparer.OrdinalIgnoreCase);

        var items = legacyItems
            .Select(legacy =>
            {
                var migrated = migrationMap.GetValueOrDefault(legacy.Id.ToString());
                return new TeamAgentLegacyMigrationStatusItem(
                    legacy.Id,
                    legacy.Name,
                    legacy.Mode.ToString(),
                    migrated is null ? "pending" : "migrated",
                    migrated?.Id,
                    migrated?.Name,
                    migrated?.CreatedAt,
                    "/api/v1/team-agents",
                    "2026-10-01");
            })
            .OrderBy(item => item.MigrationStatus, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LegacyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.LegacyId)
            .ToList();

        var migratedCount = items.Count(item => string.Equals(item.MigrationStatus, "migrated", StringComparison.OrdinalIgnoreCase));
        return new TeamAgentLegacyMigrationStatusDto(items.Count, migratedCount, items.Count - migratedCount, items);
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
        var filtered = includeContextMarkers ? items : items.Where(item => !item.IsContextCleared).ToList();
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
        if (execution is null)
        {
            return null;
        }

        var steps = await _executionStepRepository.GetByExecutionIdAsync(tenantId, executionId, cancellationToken);
        return MapExecution(execution, steps);
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
        var contributions = new List<TeamAgentMemberContribution>();
        if (request.ConversationId.HasValue)
        {
            var executions = await _executionRepository.GetRecentAsync(tenantId, 20, cancellationToken);
            var matchedExecution = executions.FirstOrDefault(item => item.ConversationId == request.ConversationId.Value && item.TeamAgentId == teamAgentId);
            if (matchedExecution is not null)
            {
                var stepEntities = await _executionStepRepository.GetByExecutionIdAsync(tenantId, matchedExecution.Id, cancellationToken);
                contributions.AddRange(stepEntities
                    .Where(step => step.AgentId.HasValue && step.AgentId.Value > 0)
                    .Select((step, index) => new TeamAgentMemberContribution(
                        step.AgentId!.Value,
                        step.AgentName,
                        step.RoleName,
                        step.Alias,
                        step.InputMessage,
                        step.OutputMessage ?? string.Empty,
                        index + 1,
                        step.StartedAt,
                        step.CompletedAt ?? step.StartedAt)));
            }
        }

        var draft = _schemaDraftComposer.Compose(teamAgent, request.Requirement, contributions, appId);
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

    public async Task<IReadOnlyList<TeamAgentSchemaDraftListItem>> ListSchemaDraftsAsync(
        TenantId tenantId,
        long teamAgentId,
        CancellationToken cancellationToken)
    {
        var items = await _schemaDraftRepository.GetByTeamAgentAsync(tenantId, teamAgentId, cancellationToken);
        return items.Select(item => new TeamAgentSchemaDraftListItem(
            item.Id,
            item.TeamAgentId,
            item.ConversationId,
            item.Title,
            item.Requirement,
            item.Status.ToString().ToLowerInvariant(),
            item.ConfirmationState.ToString().ToLowerInvariant(),
            item.CreatedAt,
            item.UpdatedAt,
            item.ConfirmedAt)).ToList();
    }

    public async Task<IReadOnlyList<TeamAgentSchemaDraftExecutionAuditItem>> GetSchemaDraftExecutionAuditsAsync(
        TenantId tenantId,
        long teamAgentId,
        long draftId,
        CancellationToken cancellationToken)
    {
        _ = await RequireDraftAsync(tenantId, teamAgentId, draftId, cancellationToken);
        var items = await _schemaDraftAuditRepository.GetByDraftIdAsync(tenantId, draftId, cancellationToken);
        return items.Select(item => new TeamAgentSchemaDraftExecutionAuditItem(
            item.Id,
            item.DraftId,
            item.Sequence,
            item.Stage,
            item.Action,
            item.Status,
            item.ResourceKey,
            item.ResourceId,
            item.Detail,
            item.CreatedAt)).ToList();
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
        if (entity.ConfirmationState == TeamAgentSchemaDraftConfirmationState.Confirmed)
        {
            throw new BusinessException("已确认的草案不能再修改。", ErrorCodes.ValidationError);
        }

        if (entity.Status == TeamAgentSchemaDraftStatus.Discarded)
        {
            throw new BusinessException("已废弃的草案不能再修改。", ErrorCodes.ValidationError);
        }

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
        if (entity.Status == TeamAgentSchemaDraftStatus.Discarded)
        {
            throw new BusinessException("已废弃的草案不能再创建。", ErrorCodes.ValidationError);
        }

        var existingAudits = await _schemaDraftAuditRepository.GetByDraftIdAsync(tenantId, draftId, cancellationToken);
        var auditSequence = existingAudits.Count;
        var executionAudits = new List<TeamAgentSchemaDraftExecutionAudit>();
        var draft = DeserializeDraft(entity.DraftJson);
        if (draft.OpenQuestions.Count > 0)
        {
            throw new BusinessException("草案仍存在待确认问题，不能直接创建。", ErrorCodes.ValidationError);
        }

        if (entity.ConfirmationState == TeamAgentSchemaDraftConfirmationState.Confirmed)
        {
            var existingResponse = BuildConfirmedDraftResponse(entity);
            AddSchemaDraftAudit(
                executionAudits,
                tenantId,
                entity.Id,
                ref auditSequence,
                "confirm-create",
                "reuse_confirmed_result",
                "completed",
                null,
                null,
                "草案已确认，直接返回已创建资源。");
            await PersistSchemaDraftAuditsAsync(executionAudits, cancellationToken);
            return existingResponse;
        }

        var resources = new List<SchemaDraftCreatedResourceDto>();
        try
        {
            AddSchemaDraftAudit(
                executionAudits,
                tenantId,
                entity.Id,
                ref auditSequence,
                "confirm-create",
                "validate_draft",
                "completed",
                null,
                null,
                $"实体数={draft.Entities.Count}, 关系数={draft.Relations.Count}, 权限规则数={draft.SecurityPolicies.Count}");

            foreach (var table in draft.Entities)
            {
                var fields = draft.Fields.Where(item => string.Equals(item.TableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.SortOrder)
                    .Select(MapField)
                    .ToList();
                var indexes = draft.Indexes.Where(item => string.Equals(item.TableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                    .Select(index => new DynamicIndexDefinition(index.Name, index.IsUnique, index.Fields))
                    .ToList();
                var createRequest = new DynamicTableCreateRequest(table.TableKey, table.DisplayName, table.Description, "Sqlite", fields, indexes, entity.AppId);
                var createdId = await _dynamicTableCommandService.CreateAsync(tenantId, userId, createRequest, cancellationToken);
                resources.Add(new SchemaDraftCreatedResourceDto(table.TableKey, createdId.ToString()));
                AddSchemaDraftAudit(
                    executionAudits,
                    tenantId,
                    entity.Id,
                    ref auditSequence,
                    "create-table",
                    "create_dynamic_table",
                    "completed",
                    table.TableKey,
                    createdId.ToString(),
                    table.DisplayName);
            }

            foreach (var table in draft.Entities)
            {
                var tableRelations = draft.Relations.Where(item => string.Equals(item.SourceTableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                    .Select(relation => new DynamicRelationDefinition(relation.RelatedTableKey, relation.SourceField, relation.TargetField, relation.RelationType, relation.CascadeRule))
                    .ToList();
                if (tableRelations.Count > 0)
                {
                    await _dynamicTableCommandService.SetRelationsAsync(tenantId, userId, table.TableKey, new DynamicRelationUpsertRequest(tableRelations), cancellationToken);
                    AddSchemaDraftAudit(
                        executionAudits,
                        tenantId,
                        entity.Id,
                        ref auditSequence,
                        "set-relations",
                        "set_dynamic_relations",
                        "completed",
                        table.TableKey,
                        null,
                        $"关系数={tableRelations.Count}");
                }

                var permissions = draft.SecurityPolicies.Where(item => string.Equals(item.TableKey, table.TableKey, StringComparison.OrdinalIgnoreCase))
                    .Select(policy => new DynamicFieldPermissionRule(policy.FieldName, policy.RoleCode, policy.CanView, policy.CanEdit))
                    .ToList();
                if (permissions.Count > 0)
                {
                    await _dynamicTableCommandService.SetFieldPermissionsAsync(tenantId, userId, table.TableKey, new DynamicFieldPermissionUpsertRequest(permissions), cancellationToken);
                    AddSchemaDraftAudit(
                        executionAudits,
                        tenantId,
                        entity.Id,
                        ref auditSequence,
                        "set-permissions",
                        "set_dynamic_field_permissions",
                        "completed",
                        table.TableKey,
                        null,
                        $"权限规则数={permissions.Count}");
                }
            }

            entity.Confirm(
                JsonSerializer.Serialize(resources.Select(item => item.TableKey).ToList(), JsonOptions),
                JsonSerializer.Serialize(resources, JsonOptions));
            AddSchemaDraftAudit(
                executionAudits,
                tenantId,
                entity.Id,
                ref auditSequence,
                "confirm-create",
                "confirm_schema_draft",
                "completed",
                null,
                null,
                $"已创建 {resources.Count} 张动态表。");

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _schemaDraftRepository.UpdateAsync(entity, cancellationToken);
                await PersistSchemaDraftAuditsAsync(executionAudits, cancellationToken);
            }, cancellationToken);

            await RecordSchemaDraftAuditAsync(
                tenantId,
                userId,
                "TEAM_AGENT_SCHEMA_DRAFT_CONFIRM_CREATE",
                "Success",
                draftId.ToString(),
                $"TeamAgent={teamAgentId}; Tables={string.Join(',', resources.Select(item => item.TableKey))}",
                cancellationToken);

            return new SchemaDraftConfirmationResponse(entity.Id, entity.ConfirmationState.ToString().ToLowerInvariant(), resources.Select(item => item.TableKey).ToList(), resources);
        }
        catch (Exception ex)
        {
            AddSchemaDraftAudit(
                executionAudits,
                tenantId,
                entity.Id,
                ref auditSequence,
                "confirm-create",
                "confirm_schema_draft",
                "failed",
                null,
                null,
                ex.Message);

            var rollbackFailures = await RollbackCreatedTablesAsync(
                tenantId,
                userId,
                entity.Id,
                resources,
                executionAudits,
                auditSequence,
                cancellationToken);

            await PersistSchemaDraftAuditsAsync(executionAudits, cancellationToken);
            await RecordSchemaDraftAuditAsync(
                tenantId,
                userId,
                "TEAM_AGENT_SCHEMA_DRAFT_CONFIRM_CREATE_FAILED",
                "Failed",
                draftId.ToString(),
                rollbackFailures.Count == 0
                    ? ex.Message
                    : $"{ex.Message}; rollbackFailed={string.Join(',', rollbackFailures)}",
                cancellationToken);

            if (rollbackFailures.Count == 0)
            {
                throw new BusinessException($"SchemaDraft 创建动态表失败，已回滚：{ex.Message}", ErrorCodes.ServerError);
            }

            throw new BusinessException(
                $"SchemaDraft 创建动态表失败，且以下表回滚失败：{string.Join(',', rollbackFailures)}。原始错误：{ex.Message}",
                ErrorCodes.ServerError);
        }
    }

    public async Task DiscardSchemaDraftAsync(TenantId tenantId, long teamAgentId, long draftId, CancellationToken cancellationToken)
    {
        var entity = await RequireDraftAsync(tenantId, teamAgentId, draftId, cancellationToken);
        if (entity.ConfirmationState == TeamAgentSchemaDraftConfirmationState.Confirmed)
        {
            throw new BusinessException("已确认的草案不能废弃。", ErrorCodes.ValidationError);
        }

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

            var members = DeserializeMembers(teamAgent.MembersJson).Where(item => item.IsEnabled).OrderBy(item => item.SortOrder).ToList();
            ValidateMembers(members);
            await EnsureAgentsExistAsync(tenantId, members, linkedCts.Token);

            var runtimeResult = await _orchestrationRuntime.ExecuteAsync(
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
                            contribution.AgentId,
                            contribution.AgentName,
                            contribution.RoleName
                        }, JsonOptions),
                        "member.message",
                        contribution.RoleName,
                        false,
                        _idGeneratorAccessor.NextId());
                    conversation.AddMessage(message.CreatedAt);
                    await PersistMessageAsync(conversation, message, linkedCts.Token);
                },
                linkedCts.Token);

            steps.AddRange(runtimeResult.Steps);
            await PersistExecutionStepsAsync(tenantId, execution.Id, steps, linkedCts.Token);
            var currentMessage = runtimeResult.FinalMessage;

            if (request.GenerateSchemaDraft == true || string.Equals(teamAgent.DefaultEntrySkill, "schema_builder", StringComparison.OrdinalIgnoreCase))
            {
                draft = _schemaDraftComposer.Compose(teamAgent, request.Message, runtimeResult.Contributions, appId);
                await AddEventAsync(events, emitAsync, new TeamAgentRunEvent("schema.draft.updated", JsonSerializer.Serialize(draft, JsonOptions)), linkedCts.Token);
            }

            await AddEventAsync(events, emitAsync, new TeamAgentRunEvent("conversation.completed", JsonSerializer.Serialize(new
            {
                executionId = execution.Id,
                conversationId = conversation.Id,
                content = currentMessage
            })), linkedCts.Token);

            execution.Complete(currentMessage, JsonSerializer.Serialize(steps, JsonOptions), JsonSerializer.Serialize(events, JsonOptions));
            await _executionRepository.UpdateAsync(execution, linkedCts.Token);

            return new TeamAgentChatResponse(conversation.Id, execution.Id, currentMessage, events, draft);
        }
        catch (TeamAgentOrchestrationExecutionException ex)
        {
            steps.AddRange(ex.Steps);
            await PersistExecutionStepsAsync(tenantId, execution.Id, steps, linkedCts.Token);
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

    /// <summary>
    /// 编排完成后在后台触发增量摘要更新，失败时仅记录警告，不影响主流程。
    /// 使用新的 CancellationToken（不受主请求取消影响），保证摘要入库成功。
    /// </summary>
    private async Task PersistExecutionStepsAsync(
        TenantId tenantId,
        long executionId,
        IReadOnlyList<TeamAgentExecutionStep> steps,
        CancellationToken cancellationToken)
    {
        await _executionStepRepository.DeleteByExecutionIdAsync(tenantId, executionId, cancellationToken);
        var entities = steps.Select(step => new TeamAgentExecutionStepEntity(
                tenantId,
                executionId,
                step.AgentId.HasValue && step.AgentId.Value > 0 ? step.AgentId : null,
                step.AgentName,
                step.RoleName,
                step.Alias,
                step.InputMessage,
                step.OutputMessage,
                step.Status,
                step.ErrorMessage,
                step.StartedAt,
                step.CompletedAt,
                step.StepId > 0 ? step.StepId : _idGeneratorAccessor.NextId()))
            .ToList();
        await _executionStepRepository.AddRangeAsync(entities, cancellationToken);
    }

    private async Task PersistSchemaDraftAuditsAsync(
        IReadOnlyList<TeamAgentSchemaDraftExecutionAudit> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        await _schemaDraftAuditRepository.AddRangeAsync(items, cancellationToken);
    }

    private async Task<List<string>> RollbackCreatedTablesAsync(
        TenantId tenantId,
        long userId,
        long draftId,
        IReadOnlyList<SchemaDraftCreatedResourceDto> resources,
        List<TeamAgentSchemaDraftExecutionAudit> audits,
        int auditSequence,
        CancellationToken cancellationToken)
    {
        var failedTableKeys = new List<string>();
        var currentSequence = auditSequence;
        foreach (var resource in resources.Reverse())
        {
            try
            {
                await _dynamicTableCommandService.DeleteAsync(tenantId, userId, resource.TableKey, cancellationToken);
                AddSchemaDraftAudit(
                    audits,
                    tenantId,
                    draftId,
                    ref currentSequence,
                    "rollback",
                    "delete_dynamic_table",
                    "completed",
                    resource.TableKey,
                    resource.ResourceId,
                    "已回滚动态表。");
            }
            catch (Exception rollbackEx)
            {
                failedTableKeys.Add(resource.TableKey);
                AddSchemaDraftAudit(
                    audits,
                    tenantId,
                    draftId,
                    ref currentSequence,
                    "rollback",
                    "delete_dynamic_table",
                    "failed",
                    resource.TableKey,
                    resource.ResourceId,
                    rollbackEx.Message);
            }
        }

        return failedTableKeys;
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
        var ids = members
            .Where(member => member.AgentId.HasValue && member.AgentId.Value > 0)
            .Select(member => member.AgentId!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var agents = await _agentRepository.QueryByIdsAsync(tenantId, ids, cancellationToken);
        var missing = ids.Except(agents.Select(agent => agent.Id)).FirstOrDefault();
        if (missing > 0)
        {
            throw new BusinessException($"团队成员 Agent 不存在: {missing}", ErrorCodes.ValidationError);
        }
    }

    private static void ValidateMembers(IReadOnlyList<TeamAgentMemberItem> members)
    {
        if (members.Count == 0 || !members.Any(member => member.IsEnabled && member.AgentId.HasValue && member.AgentId.Value > 0))
        {
            throw new BusinessException("Team Agent 至少需要一个已启用且已绑定单 Agent 的成员。", ErrorCodes.ValidationError);
        }
    }

    private static TeamAgentListItem MapListItem(TeamAgent item)
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
            item.LegacySourceType,
            item.LegacySourceId,
            item.CreatedAt,
            item.UpdatedAt);
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
            entity.LegacySourceType,
            entity.LegacySourceId,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.PublishedAt,
            DeserializeMembers(entity.MembersJson));

    private static TeamAgentMemberItem MapTemplateMember(TeamAgentTemplateMember member)
        => new(
            null,
            member.RoleName,
            member.Responsibility,
            member.Alias,
            member.SortOrder,
            member.IsEnabled,
            member.PromptPrefix,
            DeserializeStringList(member.CapabilityTagsJson),
            "unbound");

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

    private static TeamAgentExecutionResult MapExecution(TeamAgentExecution entity, IReadOnlyList<TeamAgentExecutionStepEntity> steps)
        => new(
            entity.Id,
            entity.TeamAgentId,
            entity.ConversationId,
            entity.Status.ToString().ToLowerInvariant(),
            entity.OutputMessage,
            entity.ErrorMessage,
            steps.Select(step => new TeamAgentExecutionStep(
                step.Id,
                step.AgentId,
                step.AgentName,
                step.RoleName,
                step.Alias,
                step.InputMessage,
                step.OutputMessage,
                step.Status,
                step.ErrorMessage,
                step.StartedAt,
                step.CompletedAt)).ToList(),
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

    private void AddSchemaDraftAudit(
        List<TeamAgentSchemaDraftExecutionAudit> target,
        TenantId tenantId,
        long draftId,
        ref int sequence,
        string stage,
        string action,
        string status,
        string? resourceKey,
        string? resourceId,
        string? detail)
    {
        sequence++;
        target.Add(new TeamAgentSchemaDraftExecutionAudit(
            tenantId,
            draftId,
            sequence,
            stage,
            action,
            status,
            resourceKey,
            resourceId,
            detail,
            _idGeneratorAccessor.NextId()));
    }

    private async Task RecordSchemaDraftAuditAsync(
        TenantId tenantId,
        long userId,
        string action,
        string result,
        string target,
        string detail,
        CancellationToken cancellationToken)
    {
        var auditContext = new Atlas.Application.Audit.Models.AuditContext(
            tenantId,
            userId.ToString(),
            action,
            result,
            $"{target}:{detail}",
            null,
            null,
            new ClientContext(ClientType.Backend, ClientPlatform.Web, ClientChannel.Browser, ClientAgent.Other));
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);
    }

    private static SchemaDraftConfirmationResponse BuildConfirmedDraftResponse(TeamAgentSchemaDraft entity)
    {
        var resources = DeserializeCreatedResources(entity.CreatedResourcesJson);
        var tableKeys = resources.Count > 0
            ? resources.Select(item => item.TableKey).ToList()
            : DeserializeStringList(entity.CreatedTableKeysJson);
        return new SchemaDraftConfirmationResponse(
            entity.Id,
            entity.ConfirmationState.ToString().ToLowerInvariant(),
            tableKeys,
            resources);
    }

    private static string BuildPublicationSnapshot(TeamAgent entity)
    {
        var snapshot = new
        {
            teamAgent = new
            {
                entity.Id,
                entity.Name,
                entity.Description,
                entity.TeamMode,
                entity.Status,
                entity.DefaultEntrySkill,
                entity.PublishVersion,
                entity.LegacySourceType,
                entity.LegacySourceId
            },
            capabilityTags = DeserializeStringList(entity.CapabilityTagsJson),
            boundDataAssets = DeserializeStringList(entity.BoundDataAssetsJson),
            members = DeserializeMembers(entity.MembersJson),
            schemaConfig = entity.SchemaConfigJson,
            generatedAt = DateTime.UtcNow
        };
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

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
            .Select(member =>
            {
                var normalizedAgentId = member.AgentId.HasValue && member.AgentId.Value > 0 ? member.AgentId : null;
                return new TeamAgentMemberItem(
                    normalizedAgentId,
                    string.IsNullOrWhiteSpace(member.RoleName)
                        ? (normalizedAgentId.HasValue ? $"Agent-{normalizedAgentId.Value}" : "Unbound-Agent")
                        : member.RoleName.Trim(),
                    member.Responsibility?.Trim(),
                    member.Alias?.Trim(),
                    member.SortOrder,
                    member.IsEnabled && normalizedAgentId.HasValue,
                    member.PromptPrefix?.Trim(),
                    member.CapabilityTags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
                    normalizedAgentId.HasValue ? "bound" : "unbound");
            })
            .OrderBy(member => member.SortOrder)
            .ToList();

    private static string SerializeMembers(IReadOnlyList<TeamAgentMemberItem> members)
        => JsonSerializer.Serialize(members, JsonOptions);

    private static List<TeamAgentMemberItem> DeserializeMembers(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<TeamAgentMemberItem>>(json, JsonOptions) ?? [];

    private static List<MultiAgentMemberItem> DeserializeLegacyMembers(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<MultiAgentMemberItem>>(json, JsonOptions) ?? [];

    private static string SerializeStringList(IReadOnlyList<string>? values)
        => JsonSerializer.Serialize(values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [], JsonOptions);

    private static List<string> DeserializeStringList(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static List<TeamAgentRunEvent> DeserializeEvents(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<TeamAgentRunEvent>>(json, JsonOptions) ?? [];

    private static List<SchemaDraftCreatedResourceDto> DeserializeCreatedResources(string json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<SchemaDraftCreatedResourceDto>>(json, JsonOptions) ?? [];

    private static SchemaDraftDto DeserializeDraft(string json)
        => JsonSerializer.Deserialize<SchemaDraftDto>(json, JsonOptions)
           ?? new SchemaDraftDto(string.Empty, [], [], [], [], [], [], TeamAgentSchemaDraftConfirmationState.Pending.ToString().ToLowerInvariant());
}
