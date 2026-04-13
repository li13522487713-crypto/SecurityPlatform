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
    private const string DefaultSpaceId = "atlas-space";

    private readonly ISqlSugarClient _db;
    private readonly IAiAppService _aiAppService;
    private readonly IWorkflowV2CommandService _workflowCommandService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public WorkspaceIdeService(
        ISqlSugarClient db,
        IAiAppService aiAppService,
        IWorkflowV2CommandService workflowCommandService,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
        _aiAppService = aiAppService;
        _workflowCommandService = workflowCommandService;
        _idGeneratorAccessor = idGeneratorAccessor;
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
            new WorkflowV2CreateRequest(workflowName, request.Description?.Trim() ?? normalizedName, WorkflowMode.Standard),
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
