using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;
using SqlSugar;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiWorkspaceService : IAiWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string WorkflowResourceType = "workflow";
    private const string PluginResourceType = "plugin";
    private const string KnowledgeBaseResourceType = "knowledge-base";
    private const string DatabaseResourceType = "database";
    private const string AgentResourceType = "agent";
    private const string AppResourceType = "app";
    private const string PromptResourceType = "prompt";

    private readonly AiWorkspaceRepository _workspaceRepository;
    private readonly AiPluginRepository _pluginRepository;
    private readonly AiPluginApiRepository _pluginApiRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly IWorkflowMetaRepository _workflowMetaRepository;
    private readonly IWorkflowDraftRepository _workflowDraftRepository;
    private readonly IWorkflowVersionRepository _workflowVersionRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISqlSugarClient _db;

    public AiWorkspaceService(
        AiWorkspaceRepository workspaceRepository,
        AiPluginRepository pluginRepository,
        AiPluginApiRepository pluginApiRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        AiDatabaseRepository databaseRepository,
        IWorkflowMetaRepository workflowMetaRepository,
        IWorkflowDraftRepository workflowDraftRepository,
        IWorkflowVersionRepository workflowVersionRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        ISqlSugarClient db)
    {
        _workspaceRepository = workspaceRepository;
        _pluginRepository = pluginRepository;
        _pluginApiRepository = pluginApiRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _databaseRepository = databaseRepository;
        _workflowMetaRepository = workflowMetaRepository;
        _workflowDraftRepository = workflowDraftRepository;
        _workflowVersionRepository = workflowVersionRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _db = db;
    }

    public async Task<AiWorkspaceDto> GetCurrentAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.FindByUserIdAsync(tenantId, userId, cancellationToken);
        if (workspace is null)
        {
            workspace = new AiWorkspace(
                tenantId,
                userId,
                "我的 AI 工作台",
                "light",
                "/ai/workspace",
                "[]",
                _idGeneratorAccessor.NextId());
            await _workspaceRepository.AddAsync(workspace, cancellationToken);
        }

        return MapWorkspace(workspace);
    }

    public async Task<AiWorkspaceDto> UpdateAsync(
        TenantId tenantId,
        long userId,
        AiWorkspaceUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.FindByUserIdAsync(tenantId, userId, cancellationToken);
        if (workspace is null)
        {
            workspace = new AiWorkspace(
                tenantId,
                userId,
                request.Name.Trim(),
                request.Theme.Trim(),
                request.LastVisitedPath.Trim(),
                SerializeFavoriteIds(request.FavoriteResourceIds),
                _idGeneratorAccessor.NextId());
            await _workspaceRepository.AddAsync(workspace, cancellationToken);
            return MapWorkspace(workspace);
        }

        workspace.Update(
            request.Name.Trim(),
            request.Theme.Trim(),
            request.LastVisitedPath.Trim(),
            SerializeFavoriteIds(request.FavoriteResourceIds));
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        return MapWorkspace(workspace);
    }

    public async Task<AiLibraryPagedResult> GetLibraryAsync(
        TenantId tenantId,
        AiLibraryQueryRequest request,
        CancellationToken cancellationToken)
    {
        var keyword = request.Keyword?.Trim();
        var hasKeyword = !string.IsNullOrWhiteSpace(keyword);
        var resourceType = request.ResourceType?.Trim().ToLowerInvariant();
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var perTypeLimit = 50;

        var agentsQuery = _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (hasKeyword)
        {
            var safeKeyword = keyword!;
            agentsQuery = agentsQuery.Where(x => x.Name.Contains(safeKeyword) || (x.Description != null && x.Description.Contains(safeKeyword)));
        }

        var agentEntities = await agentsQuery
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .ToListAsync(cancellationToken);
        var agents = agentEntities
            .Select(x => new AiLibraryItem("agent", x.Id, x.Name, x.Description, x.UpdatedAt ?? x.CreatedAt, $"/ai/agents/{x.Id}/edit"));

        var knowledgeBasesQuery = _db.Queryable<KnowledgeBase>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (hasKeyword)
        {
            var safeKeyword = keyword!;
            knowledgeBasesQuery = knowledgeBasesQuery.Where(x => x.Name.Contains(safeKeyword) || (x.Description != null && x.Description.Contains(safeKeyword)));
        }

        var knowledgeBaseEntities = await knowledgeBasesQuery
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .ToListAsync(cancellationToken);
        var knowledgeBases = knowledgeBaseEntities
            .Select(x => new AiLibraryItem("knowledge-base", x.Id, x.Name, x.Description, x.CreatedAt, $"/ai/knowledge-bases/{x.Id}"));

        var workflowsQuery = _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted);
        if (hasKeyword)
        {
            var safeKeyword = keyword!;
            workflowsQuery = workflowsQuery.Where(x => x.Name.Contains(safeKeyword) || (x.Description != null && x.Description.Contains(safeKeyword)));
        }

        var workflowEntities = await workflowsQuery
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .ToListAsync(cancellationToken);
        var workflows = workflowEntities
            .Select(x => new AiLibraryItem(
                WorkflowResourceType,
                x.Id,
                x.Name,
                x.Description,
                x.UpdatedAt,
                x.Mode == WorkflowMode.ChatFlow
                    ? $"/chat_flow/{x.Id}/editor"
                    : $"/work_flow/{x.Id}/editor"));

        var pluginsQuery = _db.Queryable<AiPlugin>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (hasKeyword)
        {
            var safeKeyword = keyword!;
            pluginsQuery = pluginsQuery.Where(x => x.Name.Contains(safeKeyword) || (x.Description != null && x.Description.Contains(safeKeyword)));
        }

        var pluginEntities = await pluginsQuery
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .ToListAsync(cancellationToken);
        var plugins = pluginEntities
            .Select(x => new AiLibraryItem(
                PluginResourceType,
                x.Id,
                x.Name,
                x.Description,
                x.UpdatedAt ?? x.CreatedAt,
                $"/plugins/{x.Id}"));

        var databasesQuery = _db.Queryable<AiDatabase>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (hasKeyword)
        {
            var safeKeyword = keyword!;
            databasesQuery = databasesQuery.Where(x => x.Name.Contains(safeKeyword) || (x.Description != null && x.Description.Contains(safeKeyword)));
        }

        var databaseEntities = await databasesQuery
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .ToListAsync(cancellationToken);
        var databases = databaseEntities
            .Select(x => new AiLibraryItem(
                DatabaseResourceType,
                x.Id,
                x.Name,
                x.Description,
                x.UpdatedAt ?? x.CreatedAt,
                $"/databases/{x.Id}"));

        var appsQuery = _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (hasKeyword)
        {
            var safeKeyword = keyword!;
            appsQuery = appsQuery.Where(x => x.Name.Contains(safeKeyword) || (x.Description != null && x.Description.Contains(safeKeyword)));
        }

        var appEntities = await appsQuery
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .ToListAsync(cancellationToken);
        var apps = appEntities
            .Select(x => new AiLibraryItem("app", x.Id, x.Name, x.Description, x.UpdatedAt ?? x.CreatedAt, $"/ai/apps/{x.Id}/edit"));

        var promptsQuery = _db.Queryable<AiPromptTemplate>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (hasKeyword)
        {
            var safeKeyword = keyword!;
            promptsQuery = promptsQuery.Where(x =>
                x.Name.Contains(safeKeyword)
                || (x.Description != null && x.Description.Contains(safeKeyword))
                || x.Content.Contains(safeKeyword));
        }

        var promptEntities = await promptsQuery
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Take(perTypeLimit)
            .ToListAsync(cancellationToken);
        var prompts = promptEntities
            .Select(x => new AiLibraryItem("prompt", x.Id, x.Name, x.Description, x.UpdatedAt ?? x.CreatedAt, "/ai/prompts"));

        var allItems = agents
            .Concat(knowledgeBases)
            .Concat(workflows)
            .Concat(plugins)
            .Concat(databases)
            .Concat(apps)
            .Concat(prompts);

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            allItems = allItems.Where(x => string.Equals(x.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = allItems.OrderByDescending(x => x.UpdatedAt).ToList();
        var total = ordered.Count;
        var pagedItems = ordered
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToArray();
        return new AiLibraryPagedResult(pagedItems, total, pageIndex, pageSize);
    }

    public async Task<AiLibraryMutationResult> ImportLibraryItemAsync(
        TenantId tenantId,
        long userId,
        AiLibraryImportRequest request,
        CancellationToken cancellationToken)
    {
        var resourceType = NormalizeResourceType(request.ResourceType);
        if (request.LibraryItemId <= 0)
        {
            throw new BusinessException("资源库条目标识无效。", ErrorCodes.ValidationError);
        }

        return resourceType switch
        {
            WorkflowResourceType => await ImportWorkflowAsync(tenantId, userId, request.LibraryItemId, cancellationToken),
            PluginResourceType => await ImportPluginAsync(tenantId, request.LibraryItemId, cancellationToken),
            KnowledgeBaseResourceType => await ImportKnowledgeBaseAsync(tenantId, request.LibraryItemId, cancellationToken),
            DatabaseResourceType => await ImportDatabaseAsync(tenantId, request.LibraryItemId, cancellationToken),
            _ => throw new BusinessException("当前资源类型暂不支持从资源库导入。", ErrorCodes.ValidationError)
        };
    }

    public async Task<AiLibraryMutationResult> ExportLibraryItemAsync(
        TenantId tenantId,
        long userId,
        AiLibraryExportRequest request,
        CancellationToken cancellationToken)
    {
        _ = userId;
        return await EnsureLibraryResourceExistsAsync(tenantId, request.ResourceType, request.ResourceId, cancellationToken);
    }

    public async Task<AiLibraryMutationResult> MoveLibraryItemAsync(
        TenantId tenantId,
        long userId,
        AiLibraryMoveRequest request,
        CancellationToken cancellationToken)
    {
        _ = userId;
        return await EnsureLibraryResourceExistsAsync(tenantId, request.ResourceType, request.ResourceId, cancellationToken);
    }

    private static AiWorkspaceDto MapWorkspace(AiWorkspace workspace)
        => new(
            workspace.Id,
            workspace.Name,
            workspace.Theme,
            workspace.LastVisitedPath,
            ParseFavoriteIds(workspace.FavoriteResourceIdsJson),
            workspace.CreatedAt,
            workspace.UpdatedAt);

    private static long[] ParseFavoriteIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<long[]>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string SerializeFavoriteIds(long[] favoriteIds)
    {
        var normalized = favoriteIds
            .Where(x => x > 0)
            .Distinct()
            .Take(200)
            .ToArray();
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static string NormalizeResourceType(string resourceType)
    {
        return resourceType?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private async Task<AiLibraryMutationResult> ImportWorkflowAsync(
        TenantId tenantId,
        long userId,
        long workflowId,
        CancellationToken cancellationToken)
    {
        var meta = await _workflowMetaRepository.FindActiveByIdAsync(tenantId, workflowId, cancellationToken)
            ?? throw new BusinessException("工作流资源不存在。", ErrorCodes.NotFound);
        var draft = await _workflowDraftRepository.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        var latestVersion = draft is null
            ? await _workflowVersionRepository.GetLatestAsync(tenantId, workflowId, cancellationToken)
            : null;

        var importedName = await GenerateUniqueNameAsync(
            tenantId,
            $"{meta.Name}-导入",
            (name, ct) => WorkflowNameExistsAsync(tenantId, name, ct),
            cancellationToken);
        var clonedMeta = new WorkflowMeta(
            tenantId,
            importedName,
            meta.Description,
            meta.Mode,
            userId,
            _idGeneratorAccessor.NextId());
        var canvasJson = draft?.CanvasJson ?? latestVersion?.CanvasJson ?? BuildMinimalStarterCanvasJson();
        var clonedDraft = new WorkflowDraft(tenantId, clonedMeta.Id, canvasJson, _idGeneratorAccessor.NextId());

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _workflowMetaRepository.AddAsync(clonedMeta, cancellationToken);
            await _workflowDraftRepository.AddAsync(clonedDraft, cancellationToken);
        }, cancellationToken);

        return new AiLibraryMutationResult(clonedMeta.Id, WorkflowResourceType, workflowId);
    }

    private async Task<AiLibraryMutationResult> ImportPluginAsync(
        TenantId tenantId,
        long pluginId,
        CancellationToken cancellationToken)
    {
        var plugin = await _pluginRepository.FindByIdAsync(tenantId, pluginId, cancellationToken)
            ?? throw new BusinessException("插件资源不存在。", ErrorCodes.NotFound);
        var apis = await _pluginApiRepository.GetByPluginIdAsync(tenantId, pluginId, cancellationToken);
        var importedName = await GenerateUniqueNameAsync(
            tenantId,
            $"{plugin.Name}-导入",
            (name, ct) => _pluginRepository.ExistsByNameAsync(tenantId, name, null, ct),
            cancellationToken);
        var clonedPlugin = new AiPlugin(
            tenantId,
            importedName,
            plugin.Description,
            plugin.Icon,
            plugin.Category,
            plugin.Type,
            plugin.DefinitionJson,
            plugin.SourceType,
            plugin.AuthType,
            plugin.AuthConfigJson,
            plugin.ToolSchemaJson,
            plugin.OpenApiSpecJson,
            _idGeneratorAccessor.NextId());
        var clonedApis = apis
            .Select(api => new AiPluginApi(
                tenantId,
                clonedPlugin.Id,
                api.Name,
                api.Description,
                api.Method,
                api.Path,
                api.RequestSchemaJson,
                api.ResponseSchemaJson,
                api.TimeoutSeconds,
                _idGeneratorAccessor.NextId()))
            .ToArray();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _pluginRepository.AddAsync(clonedPlugin, cancellationToken);
            await _pluginApiRepository.AddRangeAsync(clonedApis, cancellationToken);
        }, cancellationToken);

        return new AiLibraryMutationResult(clonedPlugin.Id, PluginResourceType, pluginId);
    }

    private async Task<AiLibraryMutationResult> ImportKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        var knowledgeBase = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库资源不存在。", ErrorCodes.NotFound);
        var importedName = await GenerateUniqueNameAsync(
            tenantId,
            $"{knowledgeBase.Name}-导入",
            (name, ct) => _knowledgeBaseRepository.ExistsByNameAsync(tenantId, name, null, ct),
            cancellationToken);
        var clonedKnowledgeBase = new KnowledgeBase(
            tenantId,
            importedName,
            knowledgeBase.Description,
            knowledgeBase.Type,
            _idGeneratorAccessor.NextId());
        await _knowledgeBaseRepository.AddAsync(clonedKnowledgeBase, cancellationToken);
        return new AiLibraryMutationResult(clonedKnowledgeBase.Id, KnowledgeBaseResourceType, knowledgeBaseId);
    }

    private async Task<AiLibraryMutationResult> ImportDatabaseAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken)
    {
        var database = await _databaseRepository.FindByIdAsync(tenantId, databaseId, cancellationToken)
            ?? throw new BusinessException("数据库资源不存在。", ErrorCodes.NotFound);
        var importedName = await GenerateUniqueNameAsync(
            tenantId,
            $"{database.Name}-导入",
            (name, ct) => _databaseRepository.ExistsByNameAsync(tenantId, name, null, ct),
            cancellationToken);
        var clonedDatabase = new AiDatabase(
            tenantId,
            importedName,
            database.Description,
            database.BotId,
            database.TableSchema,
            _idGeneratorAccessor.NextId());
        await _databaseRepository.AddAsync(clonedDatabase, cancellationToken);
        return new AiLibraryMutationResult(clonedDatabase.Id, DatabaseResourceType, databaseId);
    }

    private async Task<AiLibraryMutationResult> EnsureLibraryResourceExistsAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        if (resourceId <= 0)
        {
            throw new BusinessException("资源标识无效。", ErrorCodes.ValidationError);
        }

        var normalizedType = NormalizeResourceType(resourceType);
        switch (normalizedType)
        {
            case WorkflowResourceType:
                _ = await _workflowMetaRepository.FindActiveByIdAsync(tenantId, resourceId, cancellationToken)
                    ?? throw new BusinessException("工作流资源不存在。", ErrorCodes.NotFound);
                break;
            case PluginResourceType:
                _ = await _pluginRepository.FindByIdAsync(tenantId, resourceId, cancellationToken)
                    ?? throw new BusinessException("插件资源不存在。", ErrorCodes.NotFound);
                break;
            case KnowledgeBaseResourceType:
                _ = await _knowledgeBaseRepository.FindByIdAsync(tenantId, resourceId, cancellationToken)
                    ?? throw new BusinessException("知识库资源不存在。", ErrorCodes.NotFound);
                break;
            case DatabaseResourceType:
                _ = await _databaseRepository.FindByIdAsync(tenantId, resourceId, cancellationToken)
                    ?? throw new BusinessException("数据库资源不存在。", ErrorCodes.NotFound);
                break;
            case AgentResourceType:
                _ = await _db.Queryable<Agent>()
                    .Where(x => x.TenantIdValue == tenantId.Value && x.Id == resourceId)
                    .FirstAsync(cancellationToken)
                    ?? throw new BusinessException("Agent 资源不存在。", ErrorCodes.NotFound);
                break;
            case AppResourceType:
                _ = await _db.Queryable<AiApp>()
                    .Where(x => x.TenantIdValue == tenantId.Value && x.Id == resourceId)
                    .FirstAsync(cancellationToken)
                    ?? throw new BusinessException("应用资源不存在。", ErrorCodes.NotFound);
                break;
            case PromptResourceType:
                _ = await _db.Queryable<AiPromptTemplate>()
                    .Where(x => x.TenantIdValue == tenantId.Value && x.Id == resourceId)
                    .FirstAsync(cancellationToken)
                    ?? throw new BusinessException("提示词资源不存在。", ErrorCodes.NotFound);
                break;
            default:
                throw new BusinessException("当前资源类型暂不支持资源库动作。", ErrorCodes.ValidationError);
        }

        return new AiLibraryMutationResult(resourceId, normalizedType, resourceId);
    }

    private async Task<string> GenerateUniqueNameAsync(
        TenantId tenantId,
        string preferredName,
        Func<string, CancellationToken, Task<bool>> existsAsync,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        var baseName = string.IsNullOrWhiteSpace(preferredName) ? "导入资源" : preferredName.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (await existsAsync(candidate, cancellationToken))
        {
            candidate = $"{baseName}_{suffix}";
            suffix += 1;
        }

        return candidate;
    }

    private async Task<bool> WorkflowNameExistsAsync(TenantId tenantId, string name, CancellationToken cancellationToken)
    {
        return await _db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && x.Name == name)
            .CountAsync(cancellationToken) > 0;
    }

    private static string BuildMinimalStarterCanvasJson()
    {
        return JsonSerializer.Serialize(new
        {
            nodes = new object[]
            {
                new
                {
                    key = "entry_1",
                    type = (int)WorkflowNodeType.Entry,
                    label = "开始",
                    config = new
                    {
                        entryVariable = "USER_INPUT",
                        entryAutoSaveHistory = true
                    },
                    layout = new
                    {
                        x = 160,
                        y = 120,
                        width = 360,
                        height = 160
                    }
                },
                new
                {
                    key = "exit_1",
                    type = (int)WorkflowNodeType.Exit,
                    label = "结束",
                    config = new
                    {
                        exitTerminateMode = "return",
                        exitTemplate = "{{entry_1.USER_INPUT}}"
                    },
                    layout = new
                    {
                        x = 720,
                        y = 120,
                        width = 360,
                        height = 160
                    }
                }
            },
            connections = new object[]
            {
                new
                {
                    sourceNodeKey = "entry_1",
                    sourcePort = "output",
                    targetNodeKey = "exit_1",
                    targetPort = "input",
                    condition = (string?)null
                }
            },
            schemaVersion = 2,
            viewport = new
            {
                x = 0,
                y = 0,
                zoom = 100
            },
            globals = new { }
        });
    }
}
