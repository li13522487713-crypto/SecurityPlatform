using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using System.Globalization;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class WorkspaceIdeService : IWorkspaceIdeService
{
    private const string AppResourceType = "app";
    private const string AgentResourceType = "agent";
    private const string WorkflowResourceType = "workflow";
    private const string ChatflowResourceType = "chatflow";
    private const string PluginResourceType = "plugin";
    private const string KnowledgeBaseResourceType = "knowledge-base";
    private const string DatabaseResourceType = "database";
    private const string ModelConfigResourceType = "model-config";
    private const string VariableResourceType = "variable";
    private const string DefaultSpaceId = "atlas-space";

    private readonly ISqlSugarClient _db;
    private readonly IAiAppService _aiAppService;
    private readonly IDagWorkflowCommandService _workflowCommandService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public WorkspaceIdeService(
        ISqlSugarClient db,
        IAiAppService aiAppService,
        IDagWorkflowCommandService workflowCommandService,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
        _aiAppService = aiAppService;
        _workflowCommandService = workflowCommandService;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<WorkspaceIdeDashboardStatsResponse> GetDashboardStatsAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var agentCountTask = _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var appCountTask = _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var workflowCountTask = _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && x.Mode == WorkflowMode.Standard)
            .CountAsync(cancellationToken);
        var enabledModelCountTask = _db.Queryable<ModelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsEnabled)
            .CountAsync(cancellationToken);
        var pluginCountTask = _db.Queryable<AiPlugin>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var knowledgeBaseCountTask = _db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var pendingPublishTask = LoadPendingPublishItemsAsync(tenantId, top: 16, cancellationToken);
        var recentActivitiesTask = LoadRecentActivitiesAsync(tenantId, userId, cancellationToken);

        await Task.WhenAll(
            agentCountTask,
            appCountTask,
            workflowCountTask,
            enabledModelCountTask,
            pluginCountTask,
            knowledgeBaseCountTask,
            pendingPublishTask,
            recentActivitiesTask);

        return new WorkspaceIdeDashboardStatsResponse(
            await agentCountTask,
            await appCountTask,
            await workflowCountTask,
            await enabledModelCountTask,
            await pluginCountTask,
            await knowledgeBaseCountTask,
            await pendingPublishTask,
            await recentActivitiesTask);
    }

    public async Task<WorkspaceIdeSummaryResponse> GetSummaryAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var appCountTask = _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var agentCountTask = _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var workflowCountTask = _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && x.Mode == WorkflowMode.Standard)
            .CountAsync(cancellationToken);
        var chatflowCountTask = _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && x.Mode == WorkflowMode.ChatFlow)
            .CountAsync(cancellationToken);
        var pluginCountTask = _db.Queryable<AiPlugin>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var knowledgeCountTask = _db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var databaseCountTask = _db.Queryable<AiDatabase>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        var favoriteCountTask = _db.Queryable<WorkspaceIdeFavorite>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .CountAsync(cancellationToken);
        var recentCountTask = _db.Queryable<AiRecentEdit>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .CountAsync(cancellationToken);

        await Task.WhenAll(
            appCountTask,
            agentCountTask,
            workflowCountTask,
            chatflowCountTask,
            pluginCountTask,
            knowledgeCountTask,
            databaseCountTask,
            favoriteCountTask,
            recentCountTask);

        return new WorkspaceIdeSummaryResponse(
            await appCountTask,
            await agentCountTask,
            await workflowCountTask,
            await chatflowCountTask,
            await pluginCountTask,
            await knowledgeCountTask,
            await databaseCountTask,
            await favoriteCountTask,
            await recentCountTask);
    }

    public async Task<PagedResult<WorkspaceIdeResourceCardResponse>> GetResourcesAsync(
        TenantId tenantId,
        long userId,
        WorkspaceIdeResourceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 24 : request.PageSize, 1, 100);
        var normalizedKeyword = request.Keyword?.Trim();
        var normalizedResourceType = NormalizeResourceType(request.ResourceType);

        var favorites = await _db.Queryable<WorkspaceIdeFavorite>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ToListAsync(cancellationToken);
        var favoriteKeys = favorites
            .Select(item => BuildCompositeKey(item.ResourceType, item.ResourceId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var recentEdits = await _db.Queryable<AiRecentEdit>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var recentByKey = recentEdits
            .GroupBy(item => BuildCompositeKey(item.ResourceType, item.ResourceId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var resources = new List<WorkspaceIdeResourceCardResponse>(capacity: 256);
        resources.AddRange(await LoadAppsAsync(tenantId, normalizedKeyword, favoriteKeys, recentByKey, cancellationToken));
        resources.AddRange(await LoadAgentsAsync(tenantId, normalizedKeyword, favoriteKeys, recentByKey, cancellationToken));
        resources.AddRange(await LoadWorkflowsAsync(tenantId, normalizedKeyword, favoriteKeys, recentByKey, cancellationToken));
        resources.AddRange(await LoadPluginsAsync(tenantId, normalizedKeyword, favoriteKeys, recentByKey, cancellationToken));
        resources.AddRange(await LoadKnowledgeBasesAsync(tenantId, normalizedKeyword, favoriteKeys, recentByKey, cancellationToken));
        resources.AddRange(await LoadDatabasesAsync(tenantId, normalizedKeyword, favoriteKeys, recentByKey, cancellationToken));

        IEnumerable<WorkspaceIdeResourceCardResponse> filtered = resources;
        if (!string.IsNullOrWhiteSpace(normalizedResourceType))
        {
            filtered = filtered.Where(item => string.Equals(item.ResourceType, normalizedResourceType, StringComparison.OrdinalIgnoreCase));
        }

        if (request.FavoriteOnly)
        {
            filtered = filtered.Where(item => item.IsFavorite);
        }

        var ordered = filtered
            .OrderByDescending(item => item.LastEditedAt ?? item.LastOpenedAt ?? item.UpdatedAt)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedResult<WorkspaceIdeResourceCardResponse>(items, total, pageIndex, pageSize);
    }

    public async Task<WorkspaceIdeCreateAppResult> CreateAppAsync(
        TenantId tenantId,
        long userId,
        WorkspaceIdeCreateAppRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new BusinessException("应用名称不能为空。", ErrorCodes.ValidationError);
        }

        var workflowName = NormalizeWorkflowName(normalizedName);
        var workflowId = await _workflowCommandService.CreateAsync(
            tenantId,
            userId,
            new DagWorkflowCreateRequest(workflowName, request.Description?.Trim() ?? normalizedName, WorkflowMode.Standard),
            cancellationToken);

        var appId = await _aiAppService.CreateAsync(
            tenantId,
            new AiAppCreateRequest(
                normalizedName,
                request.Description?.Trim(),
                request.Icon?.Trim(),
                null,
                workflowId,
                null),
            cancellationToken);

        var entryRoute = BuildWorkflowEntryRoute(workflowId, WorkflowMode.Standard);
        return new WorkspaceIdeCreateAppResult(appId.ToString(), workflowId.ToString(), entryRoute);
    }

    public async Task UpdateFavoriteAsync(
        TenantId tenantId,
        long userId,
        string resourceType,
        long resourceId,
        WorkspaceIdeFavoriteUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedResourceType = NormalizeResourceType(resourceType);
        ValidateResourceKey(normalizedResourceType, resourceId);

        var existing = await _db.Queryable<WorkspaceIdeFavorite>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.ResourceType == normalizedResourceType
                && x.ResourceId == resourceId)
            .FirstAsync(cancellationToken);

        if (request.IsFavorite)
        {
            if (existing is not null)
            {
                existing.Touch();
                await _db.Updateable(existing)
                    .Where(x => x.TenantIdValue == existing.TenantIdValue && x.Id == existing.Id)
                    .ExecuteCommandAsync(cancellationToken);
                return;
            }

            var favorite = new WorkspaceIdeFavorite(
                tenantId,
                userId,
                normalizedResourceType,
                resourceId,
                _idGeneratorAccessor.NextId());
            await _db.Insertable(favorite).ExecuteCommandAsync(cancellationToken);
            return;
        }

        if (existing is null)
        {
            return;
        }

        await _db.Deleteable<WorkspaceIdeFavorite>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == existing.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task RecordActivityAsync(
        TenantId tenantId,
        long userId,
        WorkspaceIdeActivityCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedResourceType = NormalizeResourceType(request.ResourceType);
        ValidateResourceKey(normalizedResourceType, request.ResourceId);

        var title = request.ResourceTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new BusinessException("资源标题不能为空。", ErrorCodes.ValidationError);
        }

        var route = request.EntryRoute?.Trim();
        if (string.IsNullOrWhiteSpace(route))
        {
            throw new BusinessException("资源入口不能为空。", ErrorCodes.ValidationError);
        }

        var existing = await _db.Queryable<AiRecentEdit>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.UserId == userId
                && x.ResourceType == normalizedResourceType
                && x.ResourceId == request.ResourceId)
            .FirstAsync(cancellationToken);

        if (existing is not null)
        {
            existing.Refresh(title, route);
            await _db.Updateable(existing)
                .Where(x => x.TenantIdValue == existing.TenantIdValue && x.Id == existing.Id)
                .ExecuteCommandAsync(cancellationToken);
            return;
        }

        var activity = new AiRecentEdit(
            tenantId,
            userId,
            normalizedResourceType,
            request.ResourceId,
            title,
            route,
            _idGeneratorAccessor.NextId());
        await _db.Insertable(activity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> GetResourceReferencesAsync(
        TenantId tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedResourceType = NormalizeReferenceResourceType(resourceType);
        var numericResourceId = ParseResourceIdOrThrow(resourceId);

        var references = new List<WorkspaceIdeResourceReferenceResponse>(capacity: 64);
        switch (normalizedResourceType)
        {
            case ModelConfigResourceType:
            {
                var agentRefs = await _db.Queryable<Agent>()
                    .Where(x => x.TenantIdValue == tenantId.Value && x.ModelConfigId == numericResourceId)
                    .ToListAsync(cancellationToken);
                references.AddRange(agentRefs.Select(item => new WorkspaceIdeResourceReferenceResponse(
                    AgentResourceType,
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.Name,
                    "modelConfigId")));
                break;
            }

            case PluginResourceType:
                references.AddRange(await QueryAgentPluginReferencesAsync(tenantId, numericResourceId, cancellationToken));
                references.AddRange(await QueryAppResourceBindingReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                references.AddRange(await QueryWorkflowReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                break;

            case KnowledgeBaseResourceType:
                references.AddRange(await QueryAgentKnowledgeReferencesAsync(tenantId, numericResourceId, cancellationToken));
                references.AddRange(await QueryAppResourceBindingReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                references.AddRange(await QueryWorkflowReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                break;

            case DatabaseResourceType:
                references.AddRange(await QueryAgentDatabaseReferencesAsync(tenantId, numericResourceId, cancellationToken));
                references.AddRange(await QueryAppResourceBindingReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                references.AddRange(await QueryWorkflowReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                break;

            case VariableResourceType:
                references.AddRange(await QueryAgentVariableReferencesAsync(tenantId, numericResourceId, cancellationToken));
                references.AddRange(await QueryAppResourceBindingReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                references.AddRange(await QueryWorkflowReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                break;

            case WorkflowResourceType:
                references.AddRange(await QueryAgentWorkflowReferencesAsync(tenantId, numericResourceId, cancellationToken));
                references.AddRange(await QueryAppWorkflowReferencesAsync(tenantId, numericResourceId, cancellationToken));
                references.AddRange(await QueryAppResourceBindingReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                references.AddRange(await QueryWorkflowReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                break;

            case AgentResourceType:
                references.AddRange(await QueryAppAgentReferencesAsync(tenantId, numericResourceId, cancellationToken));
                references.AddRange(await QueryAppResourceBindingReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                break;

            default:
                references.AddRange(await QueryAppResourceBindingReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                references.AddRange(await QueryWorkflowReferencesAsync(tenantId, normalizedResourceType, numericResourceId, cancellationToken));
                break;
        }

        return references
            .DistinctBy(item => $"{item.ReferrerType}:{item.ReferrerId}:{item.BindingField}".ToLowerInvariant())
            .OrderBy(item => item.ReferrerType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ReferrerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.BindingField, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<WorkspaceIdePublishCenterItemResponse>> GetPublishCenterItemsAsync(
        TenantId tenantId,
        string? resourceType,
        CancellationToken cancellationToken = default)
    {
        var normalizedFilter = string.IsNullOrWhiteSpace(resourceType)
            ? string.Empty
            : NormalizeReferenceResourceType(resourceType);

        var agentsTask = _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);
        var appsTask = _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);
        var workflowsTask = _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var pluginsTask = _db.Queryable<AiPlugin>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(agentsTask, appsTask, workflowsTask, pluginsTask);

        var agents = await agentsTask;
        var apps = await appsTask;
        var workflows = await workflowsTask;
        var plugins = await pluginsTask;

        var agentIds = agents.Select(item => item.Id).Distinct().ToArray();
        var publicationTokenMap = new Dictionary<long, string>();
        if (agentIds.Length > 0)
        {
            var publications = await _db.Queryable<AgentPublication>()
                .Where(x =>
                    x.TenantIdValue == tenantId.Value
                    && x.IsActive
                    && SqlFunc.ContainsArray(agentIds, x.AgentId))
                .OrderBy(x => x.CreatedAt, OrderByType.Desc)
                .ToListAsync(cancellationToken);
            publicationTokenMap = publications
                .GroupBy(item => item.AgentId)
                .ToDictionary(group => group.Key, group => group.First().EmbedToken);
        }

        var items = new List<WorkspaceIdePublishCenterItemResponse>(
            agents.Count + apps.Count + workflows.Count + plugins.Count);

        items.AddRange(agents.Select(agent =>
        {
            var hasDraft = HasDraftChanges(agent.UpdatedAt, agent.PublishedAt);
            var currentVersion = Math.Max(0, agent.PublishVersion);
            var draftVersion = ResolveDraftVersion(currentVersion, hasDraft);
            publicationTokenMap.TryGetValue(agent.Id, out var embedToken);
            return new WorkspaceIdePublishCenterItemResponse(
                AgentResourceType,
                agent.Id.ToString(CultureInfo.InvariantCulture),
                agent.Name,
                currentVersion,
                draftVersion,
                agent.PublishedAt,
                ResolvePublishStatus(currentVersion, hasDraft),
                $"/api/v1/agent-publications/{agent.Id.ToString(CultureInfo.InvariantCulture)}/runtime",
                embedToken);
        }));

        items.AddRange(apps.Select(app =>
        {
            var hasDraft = HasDraftChanges(app.UpdatedAt, app.PublishedAt);
            var currentVersion = Math.Max(0, app.PublishVersion);
            var draftVersion = ResolveDraftVersion(currentVersion, hasDraft);
            return new WorkspaceIdePublishCenterItemResponse(
                AppResourceType,
                app.Id.ToString(CultureInfo.InvariantCulture),
                app.Name,
                currentVersion,
                draftVersion,
                app.PublishedAt,
                ResolvePublishStatus(currentVersion, hasDraft),
                $"/api/v1/ai-apps/{app.Id.ToString(CultureInfo.InvariantCulture)}/preview-run",
                null);
        }));

        items.AddRange(workflows.Select(workflow =>
        {
            var hasDraft = workflow.Status != WorkflowLifecycleStatus.Published
                           || HasDraftChanges(workflow.UpdatedAt, workflow.PublishedAt);
            var currentVersion = Math.Max(0, workflow.LatestVersionNumber);
            var draftVersion = ResolveDraftVersion(currentVersion, hasDraft);
            return new WorkspaceIdePublishCenterItemResponse(
                WorkflowResourceType,
                workflow.Id.ToString(CultureInfo.InvariantCulture),
                workflow.Name,
                currentVersion,
                draftVersion,
                workflow.PublishedAt,
                ResolvePublishStatus(currentVersion, hasDraft),
                $"/api/v2/workflows/{workflow.Id.ToString(CultureInfo.InvariantCulture)}/run",
                null);
        }));

        items.AddRange(plugins.Select(plugin =>
        {
            var hasDraft = HasDraftChanges(plugin.UpdatedAt, plugin.PublishedAt);
            var currentVersion = Math.Max(0, plugin.PublishedVersion);
            var draftVersion = ResolveDraftVersion(currentVersion, hasDraft);
            return new WorkspaceIdePublishCenterItemResponse(
                PluginResourceType,
                plugin.Id.ToString(CultureInfo.InvariantCulture),
                plugin.Name,
                currentVersion,
                draftVersion,
                plugin.PublishedAt,
                ResolvePublishStatus(currentVersion, hasDraft),
                $"/api/v1/plugins/{plugin.Id.ToString(CultureInfo.InvariantCulture)}",
                null);
        }));

        IEnumerable<WorkspaceIdePublishCenterItemResponse> query = items;
        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            query = query.Where(item => string.Equals(item.ResourceType, normalizedFilter, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderByDescending(item => item.LastPublishedAt ?? DateTime.UnixEpoch)
            .ThenBy(item => item.ResourceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdePendingPublishItem>> LoadPendingPublishItemsAsync(
        TenantId tenantId,
        int top,
        CancellationToken cancellationToken)
    {
        var agentsTask = _db.Queryable<Agent>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && (x.PublishVersion <= 0 || x.UpdatedAt > x.PublishedAt))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(Math.Max(top, 8))
            .ToListAsync(cancellationToken);
        var appsTask = _db.Queryable<AiApp>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && (x.PublishVersion <= 0 || x.UpdatedAt > x.PublishedAt))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(Math.Max(top, 8))
            .ToListAsync(cancellationToken);
        var workflowsTask = _db.Queryable<WorkflowMeta>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && !x.IsDeleted
                && (x.Status != WorkflowLifecycleStatus.Published || x.UpdatedAt > x.PublishedAt))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(Math.Max(top, 8))
            .ToListAsync(cancellationToken);
        var pluginsTask = _db.Queryable<AiPlugin>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && (x.PublishedVersion <= 0 || x.UpdatedAt > x.PublishedAt))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(Math.Max(top, 8))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(agentsTask, appsTask, workflowsTask, pluginsTask);

        var pendingItems = new List<WorkspaceIdePendingPublishItem>();
        pendingItems.AddRange((await agentsTask).Select(item => new WorkspaceIdePendingPublishItem(
            AgentResourceType,
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.Name,
            item.UpdatedAt ?? item.CreatedAt)));
        pendingItems.AddRange((await appsTask).Select(item => new WorkspaceIdePendingPublishItem(
            AppResourceType,
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.Name,
            item.UpdatedAt ?? item.CreatedAt)));
        pendingItems.AddRange((await workflowsTask).Select(item => new WorkspaceIdePendingPublishItem(
            WorkflowResourceType,
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.Name,
            item.UpdatedAt)));
        pendingItems.AddRange((await pluginsTask).Select(item => new WorkspaceIdePendingPublishItem(
            PluginResourceType,
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.Name,
            item.UpdatedAt ?? item.CreatedAt)));

        return pendingItems
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.ResourceName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(top, 1))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceCardResponse>> LoadRecentActivitiesAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var recentEdits = await _db.Queryable<AiRecentEdit>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (recentEdits.Count == 0)
        {
            return Array.Empty<WorkspaceIdeResourceCardResponse>();
        }

        var appIds = recentEdits
            .Where(item => string.Equals(item.ResourceType, AppResourceType, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ResourceId)
            .Distinct()
            .ToArray();
        var agentIds = recentEdits
            .Where(item => string.Equals(item.ResourceType, AgentResourceType, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ResourceId)
            .Distinct()
            .ToArray();
        var workflowIds = recentEdits
            .Where(item =>
                string.Equals(item.ResourceType, WorkflowResourceType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.ResourceType, ChatflowResourceType, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ResourceId)
            .Distinct()
            .ToArray();
        var pluginIds = recentEdits
            .Where(item => string.Equals(item.ResourceType, PluginResourceType, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ResourceId)
            .Distinct()
            .ToArray();
        var knowledgeIds = recentEdits
            .Where(item => string.Equals(item.ResourceType, KnowledgeBaseResourceType, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ResourceId)
            .Distinct()
            .ToArray();
        var databaseIds = recentEdits
            .Where(item => string.Equals(item.ResourceType, DatabaseResourceType, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ResourceId)
            .Distinct()
            .ToArray();

        var appsTask = appIds.Length == 0
            ? Task.FromResult(new List<AiApp>())
            : _db.Queryable<AiApp>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(appIds, x.Id))
                .ToListAsync(cancellationToken);
        var agentsTask = agentIds.Length == 0
            ? Task.FromResult(new List<Agent>())
            : _db.Queryable<Agent>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(agentIds, x.Id))
                .ToListAsync(cancellationToken);
        var workflowsTask = workflowIds.Length == 0
            ? Task.FromResult(new List<WorkflowMeta>())
            : _db.Queryable<WorkflowMeta>()
                .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && SqlFunc.ContainsArray(workflowIds, x.Id))
                .ToListAsync(cancellationToken);
        var pluginsTask = pluginIds.Length == 0
            ? Task.FromResult(new List<AiPlugin>())
            : _db.Queryable<AiPlugin>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(pluginIds, x.Id))
                .ToListAsync(cancellationToken);
        var knowledgeTask = knowledgeIds.Length == 0
            ? Task.FromResult(new List<KnowledgeBase>())
            : _db.Queryable<KnowledgeBase>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(knowledgeIds, x.Id))
                .ToListAsync(cancellationToken);
        var databasesTask = databaseIds.Length == 0
            ? Task.FromResult(new List<AiDatabase>())
            : _db.Queryable<AiDatabase>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(databaseIds, x.Id))
                .ToListAsync(cancellationToken);

        await Task.WhenAll(appsTask, agentsTask, workflowsTask, pluginsTask, knowledgeTask, databasesTask);

        var appMap = (await appsTask).ToDictionary(item => item.Id);
        var agentMap = (await agentsTask).ToDictionary(item => item.Id);
        var workflowMap = (await workflowsTask).ToDictionary(item => item.Id);
        var pluginMap = (await pluginsTask).ToDictionary(item => item.Id);
        var knowledgeMap = (await knowledgeTask).ToDictionary(item => item.Id);
        var databaseMap = (await databasesTask).ToDictionary(item => item.Id);

        var result = new List<WorkspaceIdeResourceCardResponse>(recentEdits.Count);
        foreach (var recent in recentEdits)
        {
            var normalizedType = NormalizeResourceType(recent.ResourceType);
            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                continue;
            }

            if (normalizedType == AppResourceType && appMap.TryGetValue(recent.ResourceId, out var app))
            {
                result.Add(new WorkspaceIdeResourceCardResponse(
                    AppResourceType,
                    app.Id.ToString(CultureInfo.InvariantCulture),
                    app.Name,
                    NullIfEmpty(app.Description),
                    NullIfEmpty(app.Icon),
                    app.Status.ToString(),
                    app.Status == AiAppStatus.Published ? "published" : "draft",
                    app.UpdatedAt ?? app.CreatedAt,
                    false,
                    recent.CreatedAt,
                    recent.UpdatedAt ?? recent.CreatedAt,
                    recent.ResourcePath ?? BuildAppEntryRoute(app),
                    app.WorkflowId.HasValue ? $"v{app.PublishVersion}" : "App",
                    app.WorkflowId?.ToString(CultureInfo.InvariantCulture)));
                continue;
            }

            if (normalizedType == AgentResourceType && agentMap.TryGetValue(recent.ResourceId, out var agent))
            {
                result.Add(new WorkspaceIdeResourceCardResponse(
                    AgentResourceType,
                    agent.Id.ToString(CultureInfo.InvariantCulture),
                    agent.Name,
                    NullIfEmpty(agent.Description),
                    NullIfEmpty(agent.AvatarUrl),
                    agent.Status.ToString(),
                    agent.PublishVersion > 0 ? "published" : "draft",
                    agent.UpdatedAt ?? agent.CreatedAt,
                    false,
                    recent.CreatedAt,
                    recent.UpdatedAt ?? recent.CreatedAt,
                    recent.ResourcePath ?? BuildAgentEntryRoute(agent.Id),
                    NullIfEmpty(agent.ModelName) ?? "Bot"));
                continue;
            }

            if ((normalizedType == WorkflowResourceType || normalizedType == ChatflowResourceType)
                && workflowMap.TryGetValue(recent.ResourceId, out var workflow))
            {
                var workflowType = workflow.Mode == WorkflowMode.ChatFlow ? ChatflowResourceType : WorkflowResourceType;
                result.Add(new WorkspaceIdeResourceCardResponse(
                    workflowType,
                    workflow.Id.ToString(CultureInfo.InvariantCulture),
                    workflow.Name,
                    NullIfEmpty(workflow.Description),
                    null,
                    workflow.Status.ToString(),
                    workflow.Status == WorkflowLifecycleStatus.Published ? "published" : "draft",
                    workflow.UpdatedAt,
                    false,
                    recent.CreatedAt,
                    recent.UpdatedAt ?? recent.CreatedAt,
                    recent.ResourcePath ?? BuildWorkflowEntryRoute(workflow.Id, workflow.Mode),
                    $"v{workflow.LatestVersionNumber}"));
                continue;
            }

            if (normalizedType == PluginResourceType && pluginMap.TryGetValue(recent.ResourceId, out var plugin))
            {
                result.Add(new WorkspaceIdeResourceCardResponse(
                    PluginResourceType,
                    plugin.Id.ToString(CultureInfo.InvariantCulture),
                    plugin.Name,
                    NullIfEmpty(plugin.Description),
                    NullIfEmpty(plugin.Icon),
                    plugin.Status.ToString(),
                    plugin.Status == AiPluginStatus.Published ? "published" : "draft",
                    plugin.UpdatedAt ?? plugin.CreatedAt,
                    false,
                    recent.CreatedAt,
                    recent.UpdatedAt ?? recent.CreatedAt,
                    recent.ResourcePath ?? BuildDevelopFocusRoute("plugins"),
                    NullIfEmpty(plugin.Category) ?? "Plugin"));
                continue;
            }

            if (normalizedType == KnowledgeBaseResourceType && knowledgeMap.TryGetValue(recent.ResourceId, out var knowledgeBase))
            {
                result.Add(new WorkspaceIdeResourceCardResponse(
                    KnowledgeBaseResourceType,
                    knowledgeBase.Id.ToString(CultureInfo.InvariantCulture),
                    knowledgeBase.Name,
                    NullIfEmpty(knowledgeBase.Description),
                    null,
                    knowledgeBase.Type.ToString(),
                    "available",
                    knowledgeBase.CreatedAt,
                    false,
                    recent.CreatedAt,
                    recent.UpdatedAt ?? recent.CreatedAt,
                    recent.ResourcePath ?? BuildDevelopFocusRoute("data"),
                    $"{knowledgeBase.DocumentCount} docs"));
                continue;
            }

            if (normalizedType == DatabaseResourceType && databaseMap.TryGetValue(recent.ResourceId, out var database))
            {
                result.Add(new WorkspaceIdeResourceCardResponse(
                    DatabaseResourceType,
                    database.Id.ToString(CultureInfo.InvariantCulture),
                    database.Name,
                    NullIfEmpty(database.Description),
                    null,
                    database.BotId.HasValue && database.BotId.Value > 0 ? "bound" : "standalone",
                    "available",
                    database.UpdatedAt ?? database.CreatedAt,
                    false,
                    recent.CreatedAt,
                    recent.UpdatedAt ?? recent.CreatedAt,
                    recent.ResourcePath ?? BuildDevelopFocusRoute("data"),
                    $"{database.RecordCount} rows"));
            }
        }

        return result.ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAgentPluginReferencesAsync(
        TenantId tenantId,
        long pluginId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<AgentPluginBinding, Agent>((binding, agent) =>
                new JoinQueryInfos(JoinType.Inner, binding.AgentId == agent.Id))
            .Where((binding, agent) =>
                binding.TenantIdValue == tenantId.Value
                && agent.TenantIdValue == tenantId.Value
                && binding.PluginId == pluginId)
            .Select((binding, agent) => new { agent.Id, agent.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                AgentResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                "pluginBindings"))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAgentKnowledgeReferencesAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<AgentKnowledgeLink, Agent>((link, agent) =>
                new JoinQueryInfos(JoinType.Inner, link.AgentId == agent.Id))
            .Where((link, agent) =>
                link.TenantIdValue == tenantId.Value
                && agent.TenantIdValue == tenantId.Value
                && link.KnowledgeBaseId == knowledgeBaseId)
            .Select((link, agent) => new { agent.Id, agent.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                AgentResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                "knowledgeBindings"))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAgentDatabaseReferencesAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<AgentDatabaseBinding, Agent>((binding, agent) =>
                new JoinQueryInfos(JoinType.Inner, binding.AgentId == agent.Id))
            .Where((binding, agent) =>
                binding.TenantIdValue == tenantId.Value
                && agent.TenantIdValue == tenantId.Value
                && binding.DatabaseId == databaseId)
            .Select((binding, agent) => new { agent.Id, agent.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                AgentResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                "databaseBindings"))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAgentVariableReferencesAsync(
        TenantId tenantId,
        long variableId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<AgentVariableBinding, Agent>((binding, agent) =>
                new JoinQueryInfos(JoinType.Inner, binding.AgentId == agent.Id))
            .Where((binding, agent) =>
                binding.TenantIdValue == tenantId.Value
                && agent.TenantIdValue == tenantId.Value
                && binding.VariableId == variableId)
            .Select((binding, agent) => new { agent.Id, agent.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                AgentResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                "variableBindings"))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAgentWorkflowReferencesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken)
    {
        var defaultRows = await _db.Queryable<Agent>()
            .Where(item => item.TenantIdValue == tenantId.Value && item.DefaultWorkflowId == workflowId)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);
        var bindingRows = await _db.Queryable<AgentWorkflowBinding, Agent>((binding, agent) =>
                new JoinQueryInfos(JoinType.Inner, binding.AgentId == agent.Id))
            .Where((binding, agent) =>
                binding.TenantIdValue == tenantId.Value
                && agent.TenantIdValue == tenantId.Value
                && binding.WorkflowId == workflowId)
            .Select((binding, agent) => new { agent.Id, agent.Name })
            .ToListAsync(cancellationToken);

        var result = new List<WorkspaceIdeResourceReferenceResponse>(defaultRows.Count + bindingRows.Count);
        result.AddRange(defaultRows.Select(item => new WorkspaceIdeResourceReferenceResponse(
            AgentResourceType,
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.Name,
            "defaultWorkflowId")));
        result.AddRange(bindingRows.Select(item => new WorkspaceIdeResourceReferenceResponse(
            AgentResourceType,
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.Name,
            "workflowBindings")));
        return result;
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAppWorkflowReferencesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<AiApp>()
            .Where(item =>
                item.TenantIdValue == tenantId.Value
                && (item.WorkflowId == workflowId || item.PrimaryWorkflowId == workflowId))
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                AppResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                "workflowId"))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAppAgentReferencesAsync(
        TenantId tenantId,
        long agentId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<AiApp>()
            .Where(item => item.TenantIdValue == tenantId.Value && item.AgentId == agentId)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                AppResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                "agentId"))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryAppResourceBindingReferencesAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<AiAppResourceBinding, AiApp>((binding, app) =>
                new JoinQueryInfos(JoinType.Inner, binding.AppId == app.Id))
            .Where((binding, app) =>
                binding.TenantIdValue == tenantId.Value
                && app.TenantIdValue == tenantId.Value
                && binding.ResourceType == resourceType
                && binding.ResourceId == resourceId)
            .Select((binding, app) => new { app.Id, app.Name, binding.Role })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                AppResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                string.IsNullOrWhiteSpace(item.Role) ? "resourceBindings" : $"resourceBindings:{item.Role}"))
            .ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>> QueryWorkflowReferencesAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<WorkflowReference, WorkflowMeta>((reference, workflow) =>
                new JoinQueryInfos(JoinType.Inner, reference.WorkflowId == workflow.Id))
            .Where((reference, workflow) =>
                reference.TenantIdValue == tenantId.Value
                && workflow.TenantIdValue == tenantId.Value
                && !workflow.IsDeleted
                && reference.ResourceType == resourceType
                && reference.ResourceId == resourceId)
            .Select((reference, workflow) => new
            {
                workflow.Id,
                workflow.Name,
                reference.NodeKey,
                reference.PortKey
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new WorkspaceIdeResourceReferenceResponse(
                WorkflowResourceType,
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                string.IsNullOrWhiteSpace(item.PortKey) ? item.NodeKey : $"{item.NodeKey}.{item.PortKey}"))
            .ToArray();
    }

    private static long ParseResourceIdOrThrow(string resourceId)
    {
        if (!long.TryParse(resourceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId) || parsedId <= 0)
        {
            throw new BusinessException("资源标识无效。", ErrorCodes.ValidationError);
        }

        return parsedId;
    }

    private static string NormalizeReferenceResourceType(string resourceType)
    {
        var normalized = NormalizeResourceType(resourceType);
        return normalized switch
        {
            "knowledge" => KnowledgeBaseResourceType,
            "model" => ModelConfigResourceType,
            "model-config" => ModelConfigResourceType,
            ChatflowResourceType => WorkflowResourceType,
            _ => normalized
        };
    }

    private static bool HasDraftChanges(DateTime? updatedAt, DateTime? publishedAt)
    {
        if (!publishedAt.HasValue || publishedAt.Value <= DateTime.UnixEpoch)
        {
            return true;
        }

        if (!updatedAt.HasValue)
        {
            return false;
        }

        return updatedAt.Value > publishedAt.Value;
    }

    private static int ResolveDraftVersion(int currentVersion, bool hasDraft)
    {
        if (currentVersion <= 0)
        {
            return 1;
        }

        return hasDraft ? currentVersion + 1 : currentVersion;
    }

    private static string ResolvePublishStatus(int currentVersion, bool hasDraft)
    {
        if (currentVersion <= 0)
        {
            return "draft";
        }

        return hasDraft ? "outdated" : "published";
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceCardResponse>> LoadAppsAsync(
        TenantId tenantId,
        string? keyword,
        HashSet<string> favoriteKeys,
        IReadOnlyDictionary<string, AiRecentEdit> recentByKey,
        CancellationToken cancellationToken)
    {
        var entities = await _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || x.Description!.Contains(keyword!))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(80)
            .ToListAsync(cancellationToken);

        return entities.Select(entity =>
        {
            var resourceKey = BuildCompositeKey(AppResourceType, entity.Id);
            recentByKey.TryGetValue(resourceKey, out var recent);
            return new WorkspaceIdeResourceCardResponse(
                AppResourceType,
                entity.Id.ToString(),
                entity.Name,
                NullIfEmpty(entity.Description),
                NullIfEmpty(entity.Icon),
                entity.Status.ToString(),
                entity.Status == AiAppStatus.Published ? "published" : "draft",
                entity.UpdatedAt ?? entity.CreatedAt,
                favoriteKeys.Contains(resourceKey),
                recent?.CreatedAt,
                recent?.UpdatedAt ?? recent?.CreatedAt,
                recent?.ResourcePath ?? BuildAppEntryRoute(entity),
                entity.WorkflowId.HasValue ? $"v{entity.PublishVersion}" : "App",
                entity.WorkflowId?.ToString());
        }).ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceCardResponse>> LoadAgentsAsync(
        TenantId tenantId,
        string? keyword,
        HashSet<string> favoriteKeys,
        IReadOnlyDictionary<string, AiRecentEdit> recentByKey,
        CancellationToken cancellationToken)
    {
        var entities = await _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || x.Description!.Contains(keyword!))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(80)
            .ToListAsync(cancellationToken);

        return entities.Select(entity =>
        {
            var resourceKey = BuildCompositeKey(AgentResourceType, entity.Id);
            recentByKey.TryGetValue(resourceKey, out var recent);
            return new WorkspaceIdeResourceCardResponse(
                AgentResourceType,
                entity.Id.ToString(),
                entity.Name,
                NullIfEmpty(entity.Description),
                NullIfEmpty(entity.AvatarUrl),
                entity.Status.ToString(),
                entity.PublishVersion > 0 ? "published" : "draft",
                entity.UpdatedAt ?? entity.CreatedAt,
                favoriteKeys.Contains(resourceKey),
                recent?.CreatedAt,
                recent?.UpdatedAt ?? recent?.CreatedAt,
                recent?.ResourcePath ?? BuildAgentEntryRoute(entity.Id),
                NullIfEmpty(entity.ModelName) ?? "Bot");
        }).ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceCardResponse>> LoadWorkflowsAsync(
        TenantId tenantId,
        string? keyword,
        HashSet<string> favoriteKeys,
        IReadOnlyDictionary<string, AiRecentEdit> recentByKey,
        CancellationToken cancellationToken)
    {
        var entities = await _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || x.Description!.Contains(keyword!))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(120)
            .ToListAsync(cancellationToken);

        return entities.Select(entity =>
        {
            var resourceType = entity.Mode == WorkflowMode.ChatFlow ? ChatflowResourceType : WorkflowResourceType;
            var resourceKey = BuildCompositeKey(resourceType, entity.Id);
            recentByKey.TryGetValue(resourceKey, out var recent);
            return new WorkspaceIdeResourceCardResponse(
                resourceType,
                entity.Id.ToString(),
                entity.Name,
                NullIfEmpty(entity.Description),
                null,
                entity.Status.ToString(),
                entity.Status == WorkflowLifecycleStatus.Published ? "published" : "draft",
                entity.UpdatedAt,
                favoriteKeys.Contains(resourceKey),
                recent?.CreatedAt,
                recent?.UpdatedAt ?? recent?.CreatedAt,
                recent?.ResourcePath ?? BuildWorkflowEntryRoute(entity.Id, entity.Mode),
                $"v{entity.LatestVersionNumber}");
        }).ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceCardResponse>> LoadPluginsAsync(
        TenantId tenantId,
        string? keyword,
        HashSet<string> favoriteKeys,
        IReadOnlyDictionary<string, AiRecentEdit> recentByKey,
        CancellationToken cancellationToken)
    {
        var entities = await _db.Queryable<AiPlugin>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || x.Description!.Contains(keyword!) || x.Category!.Contains(keyword!))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(80)
            .ToListAsync(cancellationToken);

        return entities.Select(entity =>
        {
            var resourceKey = BuildCompositeKey(PluginResourceType, entity.Id);
            recentByKey.TryGetValue(resourceKey, out var recent);
            return new WorkspaceIdeResourceCardResponse(
                PluginResourceType,
                entity.Id.ToString(),
                entity.Name,
                NullIfEmpty(entity.Description),
                NullIfEmpty(entity.Icon),
                entity.Status.ToString(),
                entity.Status == AiPluginStatus.Published ? "published" : "draft",
                entity.UpdatedAt ?? entity.CreatedAt,
                favoriteKeys.Contains(resourceKey),
                recent?.CreatedAt,
                recent?.UpdatedAt ?? recent?.CreatedAt,
                recent?.ResourcePath ?? BuildDevelopFocusRoute("plugins"),
                NullIfEmpty(entity.Category) ?? "Plugin");
        }).ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceCardResponse>> LoadKnowledgeBasesAsync(
        TenantId tenantId,
        string? keyword,
        HashSet<string> favoriteKeys,
        IReadOnlyDictionary<string, AiRecentEdit> recentByKey,
        CancellationToken cancellationToken)
    {
        var entities = await _db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || x.Description!.Contains(keyword!))
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(80)
            .ToListAsync(cancellationToken);

        return entities.Select(entity =>
        {
            var resourceKey = BuildCompositeKey(KnowledgeBaseResourceType, entity.Id);
            recentByKey.TryGetValue(resourceKey, out var recent);
            return new WorkspaceIdeResourceCardResponse(
                KnowledgeBaseResourceType,
                entity.Id.ToString(),
                entity.Name,
                NullIfEmpty(entity.Description),
                null,
                entity.Type.ToString(),
                "available",
                entity.CreatedAt,
                favoriteKeys.Contains(resourceKey),
                recent?.CreatedAt,
                recent?.UpdatedAt ?? recent?.CreatedAt,
                recent?.ResourcePath ?? BuildDevelopFocusRoute("data"),
                $"{entity.DocumentCount} docs");
        }).ToArray();
    }

    private async Task<IReadOnlyList<WorkspaceIdeResourceCardResponse>> LoadDatabasesAsync(
        TenantId tenantId,
        string? keyword,
        HashSet<string> favoriteKeys,
        IReadOnlyDictionary<string, AiRecentEdit> recentByKey,
        CancellationToken cancellationToken)
    {
        var entities = await _db.Queryable<AiDatabase>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || x.Description!.Contains(keyword!))
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(80)
            .ToListAsync(cancellationToken);

        return entities.Select(entity =>
        {
            var resourceKey = BuildCompositeKey(DatabaseResourceType, entity.Id);
            recentByKey.TryGetValue(resourceKey, out var recent);
            return new WorkspaceIdeResourceCardResponse(
                DatabaseResourceType,
                entity.Id.ToString(),
                entity.Name,
                NullIfEmpty(entity.Description),
                null,
                entity.BotId.HasValue && entity.BotId.Value > 0 ? "bound" : "standalone",
                "available",
                entity.UpdatedAt ?? entity.CreatedAt,
                favoriteKeys.Contains(resourceKey),
                recent?.CreatedAt,
                recent?.UpdatedAt ?? recent?.CreatedAt,
                recent?.ResourcePath ?? BuildDevelopFocusRoute("data"),
                $"{entity.RecordCount} rows");
        }).ToArray();
    }

    private static void ValidateResourceKey(string resourceType, long resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new BusinessException("资源类型不能为空。", ErrorCodes.ValidationError);
        }

        if (resourceId <= 0)
        {
            throw new BusinessException("资源标识无效。", ErrorCodes.ValidationError);
        }
    }

    private static string NormalizeResourceType(string? resourceType)
        => resourceType?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string BuildCompositeKey(string resourceType, long resourceId)
        => $"{resourceType}:{resourceId}";

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildAgentEntryRoute(long resourceId)
        => $"/space/{DefaultSpaceId}/bot/{resourceId}";

    private static string BuildWorkflowEntryRoute(long resourceId, WorkflowMode mode)
        => mode == WorkflowMode.ChatFlow
            ? $"/chat_flow/{resourceId}/editor"
            : $"/work_flow/{resourceId}/editor";

    private static string BuildAppEntryRoute(AiApp entity)
    {
        if (entity.WorkflowId.HasValue && entity.WorkflowId.Value > 0)
        {
            return BuildWorkflowEntryRoute(entity.WorkflowId.Value, WorkflowMode.Standard);
        }

        return BuildDevelopFocusRoute("projects");
    }

    private static string BuildDevelopFocusRoute(string focus)
        => $"/space/{DefaultSpaceId}/develop?focus={focus}";

    private static string NormalizeWorkflowName(string source)
    {
        var chars = source
            .Where(char.IsAsciiLetterOrDigit)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToList();

        var normalized = new string(chars.ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "WorkspaceFlow";
        }

        if (!char.IsLetter(normalized[0]))
        {
            normalized = $"W{normalized}";
        }

        return normalized.Length > 30 ? normalized[..30] : normalized;
    }
}
