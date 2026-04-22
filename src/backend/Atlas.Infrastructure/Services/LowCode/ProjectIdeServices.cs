using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.LowCode.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class ProjectIdeBootstrapService : IProjectIdeBootstrapService
{
    private readonly IAppDefinitionQueryService _appQuery;
    private readonly IAppResourceCatalogService _resourceCatalog;
    private readonly IAppTemplateService _templateService;
    private readonly IAppDraftLockService _draftLockService;
    private readonly ILowCodeComponentManifestService _componentManifest;
    private readonly IAppPublishService _publishService;
    private readonly IProjectIdeDependencyGraphService _dependencyGraphService;

    public ProjectIdeBootstrapService(
        IAppDefinitionQueryService appQuery,
        IAppResourceCatalogService resourceCatalog,
        IAppTemplateService templateService,
        IAppDraftLockService draftLockService,
        ILowCodeComponentManifestService componentManifest,
        IAppPublishService publishService,
        IProjectIdeDependencyGraphService dependencyGraphService)
    {
        _appQuery = appQuery;
        _resourceCatalog = resourceCatalog;
        _templateService = templateService;
        _draftLockService = draftLockService;
        _componentManifest = componentManifest;
        _publishService = publishService;
        _dependencyGraphService = dependencyGraphService;
    }

    public async Task<ProjectIdeBootstrapDto?> GetBootstrapAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        var app = await _appQuery.GetByIdAsync(tenantId, appId, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var draft = await _appQuery.GetDraftAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        var graph = await _dependencyGraphService.GetGraphAsync(tenantId, appId, schemaJsonOverride: null, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        var componentRegistry = await _componentManifest.GetRegistryAsync(tenantId, "web", cancellationToken);
        var resourceCatalog = await _resourceCatalog.SearchAsync(tenantId, new AppResourceQuery(null, null, 1, 20), cancellationToken);
        var templates = await _templateService.SearchAsync(tenantId, null, null, null, null, 1, 10, cancellationToken);
        var versions = await _appQuery.ListVersionsAsync(tenantId, appId, includeSystemSnapshot: true, cancellationToken);
        var artifacts = await _publishService.ListAsync(tenantId, appId, cancellationToken);
        var draftLock = await _draftLockService.GetCurrentAsync(tenantId, appId, cancellationToken);
        var preview = await GetPublishPreviewAsync(tenantId, appId, schemaJsonOverride: null, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");

        return new ProjectIdeBootstrapDto(
            AppId: appId.ToString(),
            App: app,
            Draft: draft,
            ProjectedSchemaJson: graph.ProjectedSchemaJson,
            ProjectionDrift: graph.ProjectionDrift,
            Graph: graph,
            ComponentRegistry: componentRegistry,
            ResourceCatalog: resourceCatalog,
            Templates: templates,
            Versions: versions,
            Artifacts: artifacts,
            DraftLock: draftLock,
            PublishPreview: preview);
    }

    public async Task<ProjectIdeValidationResultDto> ValidateAsync(TenantId tenantId, long appId, string? schemaJsonOverride, CancellationToken cancellationToken)
    {
        var graph = await _dependencyGraphService.GetGraphAsync(tenantId, appId, schemaJsonOverride, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");

        var issues = new List<ProjectIdeValidationIssueDto>();

        if (graph.ProjectionDrift)
        {
            issues.Add(new ProjectIdeValidationIssueDto(
                Severity: "warning",
                Code: "projection_drift",
                Message: "应用草稿与页面定义存在投影漂移，建议先重新保存草稿或走 Project IDE 发布流程进行同步。",
                ReferencePath: "/pages",
                PageId: null,
                ComponentId: null,
                ResourceType: null,
                ResourceId: null));
        }

        foreach (var group in graph.Groups)
        {
            foreach (var reference in group.References)
            {
                if (!reference.Exists)
                {
                    issues.Add(new ProjectIdeValidationIssueDto(
                        Severity: "error",
                        Code: "missing_resource",
                        Message: $"引用资源不存在：{reference.ResourceType}:{reference.ResourceId}",
                        ReferencePath: reference.ReferencePath,
                        PageId: reference.PageId,
                        ComponentId: reference.ComponentId,
                        ResourceType: reference.ResourceType,
                        ResourceId: reference.ResourceId));
                    continue;
                }

                if ((reference.ResourceType is "workflow" or "chatflow") && string.IsNullOrWhiteSpace(reference.ResolvedVersion))
                {
                    issues.Add(new ProjectIdeValidationIssueDto(
                        Severity: "error",
                        Code: "unpublished_workflow",
                        Message: $"引用的 {reference.ResourceType} 尚未发布，无法参与版本冻结：{reference.ResourceId}",
                        ReferencePath: reference.ReferencePath,
                        PageId: reference.PageId,
                        ComponentId: reference.ComponentId,
                        ResourceType: reference.ResourceType,
                        ResourceId: reference.ResourceId));
                }
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(graph.ProjectedSchemaJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("pages", out var pagesEl) || pagesEl.ValueKind != JsonValueKind.Array || pagesEl.GetArrayLength() == 0)
            {
                issues.Add(new ProjectIdeValidationIssueDto(
                    Severity: "error",
                    Code: "missing_pages",
                    Message: "应用 schema 中缺少 pages 集合或 pages 为空。",
                    ReferencePath: "/pages",
                    PageId: null,
                    ComponentId: null,
                    ResourceType: null,
                    ResourceId: null));
            }
            else
            {
                foreach (var pageEl in pagesEl.EnumerateArray())
                {
                    var pageId = pageEl.TryGetProperty("id", out var pageIdEl) && pageIdEl.ValueKind == JsonValueKind.String
                        ? pageIdEl.GetString()
                        : null;
                    var pageCode = pageEl.TryGetProperty("code", out var pageCodeEl) && pageCodeEl.ValueKind == JsonValueKind.String
                        ? pageCodeEl.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(pageCode))
                    {
                        issues.Add(new ProjectIdeValidationIssueDto(
                            Severity: "error",
                            Code: "page_code_missing",
                            Message: "页面缺少 code。",
                            ReferencePath: "/pages",
                            PageId: pageId,
                            ComponentId: null,
                            ResourceType: null,
                            ResourceId: null));
                    }

                    if (!pageEl.TryGetProperty("root", out var rootEl) || rootEl.ValueKind != JsonValueKind.Object)
                    {
                        issues.Add(new ProjectIdeValidationIssueDto(
                            Severity: "error",
                            Code: "page_root_missing",
                            Message: $"页面 {pageCode ?? pageId ?? "(unknown)"} 缺少 root 根组件。",
                            ReferencePath: "/pages/root",
                            PageId: pageId,
                            ComponentId: null,
                            ResourceType: null,
                            ResourceId: null));
                        continue;
                    }

                    if (!rootEl.TryGetProperty("id", out var componentIdEl) || componentIdEl.ValueKind != JsonValueKind.String ||
                        !rootEl.TryGetProperty("type", out var componentTypeEl) || componentTypeEl.ValueKind != JsonValueKind.String)
                    {
                        issues.Add(new ProjectIdeValidationIssueDto(
                            Severity: "error",
                            Code: "root_component_invalid",
                            Message: $"页面 {pageCode ?? pageId ?? "(unknown)"} 的 root 组件缺少 id/type。",
                            ReferencePath: "/pages/root",
                            PageId: pageId,
                            ComponentId: null,
                            ResourceType: null,
                            ResourceId: null));
                    }
                }
            }

            ValidateActionContracts(root, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new ProjectIdeValidationIssueDto(
                Severity: "error",
                Code: "schema_json_invalid",
                Message: $"应用 schema 不是合法 JSON：{ex.Message}",
                ReferencePath: "/",
                PageId: null,
                ComponentId: null,
                ResourceType: null,
                ResourceId: null));
        }

        return new ProjectIdeValidationResultDto(
            IsValid: issues.All(issue => !string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase)),
            Issues: issues,
            Graph: graph);
    }

    public async Task<ProjectIdePublishPreviewDto?> GetPublishPreviewAsync(TenantId tenantId, long appId, string? schemaJsonOverride, CancellationToken cancellationToken)
    {
        var graph = await _dependencyGraphService.GetGraphAsync(tenantId, appId, schemaJsonOverride, cancellationToken);
        if (graph is null)
        {
            return null;
        }

        var warnings = new List<string>();
        if (graph.ProjectionDrift)
        {
            warnings.Add("检测到页面定义与应用草稿存在漂移，发布时将自动同步到 app-level 草稿。");
        }

        foreach (var group in graph.Groups)
        {
            var unpublished = group.References
                .Where(reference => reference.Exists
                    && (reference.ResourceType is "workflow" or "chatflow")
                    && string.IsNullOrWhiteSpace(reference.ResolvedVersion))
                .Select(reference => $"{reference.ResourceType}:{reference.ResourceId}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unpublished.Length > 0)
            {
                warnings.Add($"以下资源尚未发布，当前无法冻结版本：{string.Join(", ", unpublished)}");
            }
        }

        return new ProjectIdePublishPreviewDto(
            AppId: appId.ToString(),
            SuggestedVersionLabel: $"ide-publish-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            ResourceSnapshotJson: graph.ResourceSnapshotJson,
            MissingReferences: graph.MissingReferences,
            Warnings: warnings);
    }

    private static void ValidateActionContracts(JsonElement node, List<ProjectIdeValidationIssueDto> issues)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (node.TryGetProperty("kind", out var kindEl) && kindEl.ValueKind == JsonValueKind.String)
                {
                    var kind = kindEl.GetString();
                    switch (kind)
                    {
                        case "set_variable":
                        {
                            var targetPath = node.TryGetProperty("targetPath", out var targetPathEl) && targetPathEl.ValueKind == JsonValueKind.String
                                ? targetPathEl.GetString()
                                : null;
                            if (string.IsNullOrWhiteSpace(targetPath) ||
                                !(targetPath.StartsWith("page.", StringComparison.OrdinalIgnoreCase)
                                  || targetPath.StartsWith("app.", StringComparison.OrdinalIgnoreCase)))
                            {
                                issues.Add(new ProjectIdeValidationIssueDto(
                                    Severity: "error",
                                    Code: "set_variable_scope_invalid",
                                    Message: "set_variable.targetPath 必须以 page. 或 app. 开头。",
                                    ReferencePath: "/actions/set_variable/targetPath",
                                    PageId: null,
                                    ComponentId: null,
                                    ResourceType: null,
                                    ResourceId: null));
                            }
                            break;
                        }
                        case "call_workflow":
                            if (!node.TryGetProperty("workflowId", out var workflowIdEl) || workflowIdEl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(workflowIdEl.GetString()))
                            {
                                issues.Add(new ProjectIdeValidationIssueDto(
                                    Severity: "error",
                                    Code: "workflow_id_missing",
                                    Message: "call_workflow 缺少 workflowId。",
                                    ReferencePath: "/actions/call_workflow/workflowId",
                                    PageId: null,
                                    ComponentId: null,
                                    ResourceType: "workflow",
                                    ResourceId: null));
                            }
                            break;
                        case "call_chatflow":
                            if (!node.TryGetProperty("chatflowId", out var chatflowIdEl) || chatflowIdEl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(chatflowIdEl.GetString()))
                            {
                                issues.Add(new ProjectIdeValidationIssueDto(
                                    Severity: "error",
                                    Code: "chatflow_id_missing",
                                    Message: "call_chatflow 缺少 chatflowId。",
                                    ReferencePath: "/actions/call_chatflow/chatflowId",
                                    PageId: null,
                                    ComponentId: null,
                                    ResourceType: "chatflow",
                                    ResourceId: null));
                            }
                            if (!node.TryGetProperty("streamTarget", out var streamTargetEl) || streamTargetEl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(streamTargetEl.GetString()))
                            {
                                issues.Add(new ProjectIdeValidationIssueDto(
                                    Severity: "error",
                                    Code: "chatflow_stream_target_missing",
                                    Message: "call_chatflow 缺少 streamTarget。",
                                    ReferencePath: "/actions/call_chatflow/streamTarget",
                                    PageId: null,
                                    ComponentId: null,
                                    ResourceType: "chatflow",
                                    ResourceId: null));
                            }
                            break;
                        case "navigate":
                            if (!node.TryGetProperty("to", out var toEl) || toEl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(toEl.GetString()))
                            {
                                issues.Add(new ProjectIdeValidationIssueDto(
                                    Severity: "error",
                                    Code: "navigate_target_missing",
                                    Message: "navigate 缺少 to。",
                                    ReferencePath: "/actions/navigate/to",
                                    PageId: null,
                                    ComponentId: null,
                                    ResourceType: null,
                                    ResourceId: null));
                            }
                            break;
                        case "open_external_link":
                            if (node.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                            {
                                var url = urlEl.GetString();
                                if (!string.IsNullOrWhiteSpace(url) && !Uri.TryCreate(url, UriKind.Absolute, out _))
                                {
                                    issues.Add(new ProjectIdeValidationIssueDto(
                                        Severity: "warning",
                                        Code: "external_link_invalid",
                                        Message: $"open_external_link.url 不是合法绝对地址：{url}",
                                        ReferencePath: "/actions/open_external_link/url",
                                        PageId: null,
                                        ComponentId: null,
                                        ResourceType: null,
                                        ResourceId: null));
                                }
                            }
                            break;
                    }
                }

                foreach (var property in node.EnumerateObject())
                {
                    ValidateActionContracts(property.Value, issues);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    ValidateActionContracts(item, issues);
                }
                break;
        }
    }
}

public sealed class ProjectIdePublishOrchestrator : IProjectIdePublishOrchestrator
{
    private readonly IProjectIdeDependencyGraphService _dependencyGraphService;
    private readonly IAppDefinitionRepository _appRepo;
    private readonly IAppDefinitionCommandService _appCommand;
    private readonly IAppPublishService _publishService;

    public ProjectIdePublishOrchestrator(
        IProjectIdeDependencyGraphService dependencyGraphService,
        IAppDefinitionRepository appRepo,
        IAppDefinitionCommandService appCommand,
        IAppPublishService publishService)
    {
        _dependencyGraphService = dependencyGraphService;
        _appRepo = appRepo;
        _appCommand = appCommand;
        _publishService = publishService;
    }

    public async Task<ProjectIdePublishResultDto> PublishAsync(TenantId tenantId, long currentUserId, long appId, ProjectIdePublishRequest request, CancellationToken cancellationToken)
    {
        var graph = await _dependencyGraphService.GetGraphAsync(tenantId, appId, schemaJsonOverride: null, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        if (graph.MissingReferences > 0)
        {
            throw new BusinessException(
                ErrorCodes.ValidationError,
                $"应用仍存在 {graph.MissingReferences} 个缺失依赖，禁止发布。");
        }

        var app = await _appRepo.FindByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        if (!string.Equals(app.DraftSchemaJson, graph.ProjectedSchemaJson, StringComparison.Ordinal))
        {
            app.ReplaceDraftSchema(graph.ProjectedSchemaJson, currentUserId);
            await _appRepo.UpdateAsync(app, cancellationToken);
        }

        long versionId;
        if (!string.IsNullOrWhiteSpace(request.VersionId))
        {
            if (!long.TryParse(request.VersionId, out versionId))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"versionId 无效：{request.VersionId}");
            }
        }
        else
        {
            var label = string.IsNullOrWhiteSpace(request.VersionLabel)
                ? $"ide-publish-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
                : request.VersionLabel!;
            versionId = await _appCommand.CreateVersionSnapshotAsync(
                tenantId,
                currentUserId,
                appId,
                new AppVersionSnapshotRequest(label, request.Note, graph.ResourceSnapshotJson),
                cancellationToken);
        }

        var artifact = await _publishService.PublishAsync(
            tenantId,
            currentUserId,
            appId,
            new PublishRequest(request.Kind, versionId.ToString(), request.RendererMatrixJson),
            cancellationToken);

        return new ProjectIdePublishResultDto(
            AppId: appId.ToString(),
            VersionId: versionId.ToString(),
            ResourceSnapshotJson: graph.ResourceSnapshotJson,
            Artifact: artifact,
            Graph: graph);
    }
}

public sealed class ProjectIdeDependencyGraphService : IProjectIdeDependencyGraphService
{
    private static readonly Dictionary<string, string> ResourceFieldMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["workflowId"] = "workflow",
        ["chatflowId"] = "chatflow",
        ["pluginId"] = "plugin",
        ["promptTemplateId"] = "prompt-template",
        ["triggerId"] = "trigger",
        ["databaseInfoId"] = "database",
        ["databaseId"] = "database",
        ["knowledgeBaseId"] = "knowledge",
        ["knowledgeId"] = "knowledge"
    };

    private static readonly Regex VariablePathRegex = new(@"\b(page|app|system)\.([a-zA-Z_][a-zA-Z0-9_-]*)\b", RegexOptions.Compiled);

    private readonly IAppDefinitionRepository _appRepo;
    private readonly IPageDefinitionRepository _pageRepo;
    private readonly IAppVariableRepository _variableRepo;
    private readonly IAppDefinitionQueryService _appQuery;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<ProjectIdeDependencyGraphService> _logger;

    public ProjectIdeDependencyGraphService(
        IAppDefinitionRepository appRepo,
        IPageDefinitionRepository pageRepo,
        IAppVariableRepository variableRepo,
        IAppDefinitionQueryService appQuery,
        ISqlSugarClient db,
        ILogger<ProjectIdeDependencyGraphService> logger)
    {
        _appRepo = appRepo;
        _pageRepo = pageRepo;
        _variableRepo = variableRepo;
        _appQuery = appQuery;
        _db = db;
        _logger = logger;
    }

    public async Task<ProjectIdeGraphDto?> GetGraphAsync(TenantId tenantId, long appId, string? schemaJsonOverride, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, appId, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var projectedSchemaJson = string.IsNullOrWhiteSpace(schemaJsonOverride)
            ? await BuildProjectedSchemaJsonAsync(tenantId, app, cancellationToken)
            : schemaJsonOverride!;
        var projectionDrift = string.IsNullOrWhiteSpace(schemaJsonOverride) && !JsonStringEquals(app.DraftSchemaJson, projectedSchemaJson);

        var references = ExtractReferences(projectedSchemaJson);
        var variableList = await _variableRepo.ListByAppAsync(tenantId, appId, scope: null, cancellationToken);
        var variableLookup = variableList.ToDictionary(
            variable => $"{variable.Scope}.{variable.Code}",
            variable => variable,
            StringComparer.OrdinalIgnoreCase);
        var pageVariableLookup = ExtractPageVariables(projectedSchemaJson);

        var resolvedReferences = new List<ProjectIdeReferenceDto>(references.Count);
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = await ResolveReferenceAsync(tenantId, reference, variableLookup, pageVariableLookup, cancellationToken);
            resolvedReferences.Add(resolved);
        }

        var boundPluginReferences = await ResolveBoundPluginReferencesAsync(tenantId, appId, cancellationToken);
        resolvedReferences.AddRange(boundPluginReferences);

        var groups = resolvedReferences
            .GroupBy(reference => reference.ResourceType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProjectIdeReferenceGroupDto(group.Key, group.ToList()))
            .ToList();
        var resourceSnapshotJson = BuildResourceSnapshotJson(tenantId, variableList, resolvedReferences);

        return new ProjectIdeGraphDto(
            AppId: app.Id.ToString(),
            Code: app.Code,
            ProjectedSchemaJson: projectedSchemaJson,
            ProjectionDrift: projectionDrift,
            ResourceSnapshotJson: resourceSnapshotJson,
            Groups: groups,
            TotalReferences: resolvedReferences.Count,
            MissingReferences: resolvedReferences.Count(reference => !reference.Exists));
    }

    private async Task<IReadOnlyList<ProjectIdeReferenceDto>> ResolveBoundPluginReferencesAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        var bindings = await _db.Queryable<AiAppResourceBinding>()
            .Where(item =>
                item.TenantIdValue == tenantId.Value
                && item.AppId == appId
                && item.ResourceType == "plugin")
            .ToListAsync(cancellationToken);
        if (bindings.Count == 0)
        {
            return [];
        }

        var resourceIds = bindings.Select(item => item.ResourceId).Distinct().ToArray();
        var lowCodePluginsTask = _db.Queryable<LowCodePluginDefinition>()
            .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(resourceIds, item.Id))
            .ToListAsync(cancellationToken);
        var aiPluginsTask = _db.Queryable<AiPlugin>()
            .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(resourceIds, item.Id))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(lowCodePluginsTask, aiPluginsTask);

        var lowCodePlugins = (await lowCodePluginsTask).ToDictionary(item => item.Id);
        var aiPlugins = (await aiPluginsTask).ToDictionary(item => item.Id);

        return bindings.Select(binding =>
        {
            var referencePath = $"/bindings/plugins/{binding.Id}";
            if (lowCodePlugins.TryGetValue(binding.ResourceId, out var lowCodePlugin))
            {
                return new ProjectIdeReferenceDto(
                    ResourceType: "plugin",
                    ResourceId: lowCodePlugin.PluginId,
                    DisplayName: lowCodePlugin.Name,
                    ResolvedVersion: lowCodePlugin.LatestVersion,
                    Status: "ready",
                    Exists: true,
                    ReferencePath: referencePath,
                    PageId: null,
                    ComponentId: null);
            }

            if (aiPlugins.TryGetValue(binding.ResourceId, out var aiPlugin))
            {
                return new ProjectIdeReferenceDto(
                    ResourceType: "plugin",
                    ResourceId: $"ai:{aiPlugin.Id}",
                    DisplayName: aiPlugin.Name,
                    ResolvedVersion: aiPlugin.PublishedVersion > 0 ? $"v{aiPlugin.PublishedVersion}" : null,
                    Status: aiPlugin.Status.ToString().ToLowerInvariant(),
                    Exists: true,
                    ReferencePath: referencePath,
                    PageId: null,
                    ComponentId: null);
            }

            return new ProjectIdeReferenceDto(
                ResourceType: "plugin",
                ResourceId: binding.ResourceId.ToString(),
                DisplayName: null,
                ResolvedVersion: null,
                Status: "missing",
                Exists: false,
                ReferencePath: referencePath,
                PageId: null,
                ComponentId: null);
        }).ToList();
    }

    private async Task<string> BuildProjectedSchemaJsonAsync(TenantId tenantId, AppDefinition app, CancellationToken cancellationToken)
    {
        JsonObject rootObject;
        try
        {
            rootObject = string.IsNullOrWhiteSpace(app.DraftSchemaJson)
                ? new JsonObject()
                : (JsonNode.Parse(app.DraftSchemaJson)?.AsObject() ?? new JsonObject());
        }
        catch (JsonException)
        {
            rootObject = new JsonObject();
        }

        rootObject["schemaVersion"] = app.SchemaVersion;
        rootObject["appId"] = app.Id.ToString();
        rootObject["code"] = app.Code;
        rootObject["displayName"] = app.DisplayName;
        rootObject["description"] = app.Description;
        rootObject["defaultLocale"] = app.DefaultLocale;
        rootObject["status"] = app.Status;
        rootObject["targetTypes"] = new JsonArray(
            app.TargetTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(targetType => (JsonNode?)targetType)
                .ToArray());

        var pages = await _pageRepo.ListByAppAsync(tenantId, app.Id, cancellationToken);
        var pageArray = new JsonArray();
        foreach (var page in pages.OrderBy(page => page.OrderNo))
        {
            JsonObject pageNode;
            try
            {
                pageNode = string.IsNullOrWhiteSpace(page.SchemaJson)
                    ? new JsonObject()
                    : (JsonNode.Parse(page.SchemaJson)?.AsObject() ?? new JsonObject());
            }
            catch (JsonException)
            {
                pageNode = new JsonObject();
            }

            pageNode["id"] = page.Id.ToString();
            pageNode["code"] = page.Code;
            pageNode["displayName"] = page.DisplayName;
            pageNode["path"] = page.Path;
            pageNode["targetType"] = page.TargetType;
            pageNode["layout"] = page.Layout;
            pageNode["orderNo"] = page.OrderNo;
            pageNode["visible"] = page.IsVisible;
            pageNode["locked"] = page.IsLocked;

            if (pageNode["root"] is not JsonObject rootComponent)
            {
                rootComponent = new JsonObject
                {
                    ["id"] = $"root_{page.Code}",
                    ["type"] = "Container"
                };
                pageNode["root"] = rootComponent;
            }

            if (rootComponent["children"] is null)
            {
                rootComponent["children"] = new JsonArray();
            }

            pageArray.Add(pageNode);
        }

        rootObject["pages"] = pageArray;
        return rootObject.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static List<ExtractedReference> ExtractReferences(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var refs = new List<ExtractedReference>();
            Walk(doc.RootElement, string.Empty, currentPageId: null, currentComponentId: null, refs);
            return refs
                .GroupBy(
                    reference => $"{reference.ResourceType}::{reference.ResourceId}::{reference.ReferencePath}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, HashSet<string>> ExtractPageVariables(string schemaJson)
    {
        var lookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return lookup;
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            if (!doc.RootElement.TryGetProperty("pages", out var pagesEl) || pagesEl.ValueKind != JsonValueKind.Array)
            {
                return lookup;
            }

            foreach (var pageEl in pagesEl.EnumerateArray())
            {
                if (!pageEl.TryGetProperty("id", out var pageIdEl) || pageIdEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var pageId = pageIdEl.GetString();
                if (string.IsNullOrWhiteSpace(pageId))
                {
                    continue;
                }

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (pageEl.TryGetProperty("variables", out var varsEl) && varsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var variableEl in varsEl.EnumerateArray())
                    {
                        if (variableEl.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                        {
                            var code = codeEl.GetString();
                            if (!string.IsNullOrWhiteSpace(code))
                            {
                                set.Add(code);
                            }
                        }
                    }
                }

                lookup[pageId!] = set;
            }
        }
        catch (JsonException)
        {
            return lookup;
        }

        return lookup;
    }

    private async Task<ProjectIdeReferenceDto> ResolveReferenceAsync(
        TenantId tenantId,
        ExtractedReference reference,
        IReadOnlyDictionary<string, AppVariable> variableLookup,
        IReadOnlyDictionary<string, HashSet<string>> pageVariableLookup,
        CancellationToken cancellationToken)
    {
        switch (reference.ResourceType)
        {
            case "workflow":
            case "chatflow":
            {
                if (!long.TryParse(reference.ResourceId, out var workflowId))
                {
                    return reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "invalid-id");
                }

                var expectedMode = reference.ResourceType == "chatflow" ? WorkflowMode.ChatFlow : WorkflowMode.Standard;
                var workflow = await _db.Queryable<WorkflowMeta>()
                    .Where(item => item.TenantIdValue == tenantId.Value && item.Id == workflowId && item.IsDeleted == false && item.Mode == expectedMode)
                    .FirstAsync(cancellationToken);
                if (workflow is null)
                {
                    return reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing");
                }

                var version = workflow.LatestVersionNumber > 0 ? $"v{workflow.LatestVersionNumber}" : null;
                var status = workflow.Status.ToString().ToLowerInvariant();
                return reference.ToResolved(exists: true, displayName: workflow.Name, resolvedVersion: version, status: status);
            }
            case "database":
            {
                if (!long.TryParse(reference.ResourceId, out var databaseId))
                {
                    return reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "invalid-id");
                }

                var database = await _db.Queryable<AiDatabase>()
                    .Where(item => item.TenantIdValue == tenantId.Value && item.Id == databaseId)
                    .FirstAsync(cancellationToken);
                return database is null
                    ? reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing")
                    : reference.ToResolved(
                        exists: true,
                        displayName: database.Name,
                        resolvedVersion: database.PublishedVersion > 0 ? $"v{database.PublishedVersion}" : $"schema:{database.SchemaVersion}",
                        status: database.PublishedVersion > 0 ? "published" : "draft");
            }
            case "knowledge":
            {
                if (!long.TryParse(reference.ResourceId, out var knowledgeId))
                {
                    return reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "invalid-id");
                }

                var knowledge = await _db.Queryable<KnowledgeBase>()
                    .Where(item => item.TenantIdValue == tenantId.Value && item.Id == knowledgeId)
                    .FirstAsync(cancellationToken);
                return knowledge is null
                    ? reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing")
                    : reference.ToResolved(exists: true, displayName: knowledge.Name, resolvedVersion: null, status: "ready");
            }
            case "plugin":
            {
                if (reference.ResourceId.StartsWith("ai:", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(reference.ResourceId["ai:".Length..], out var aiPluginId))
                {
                    var aiPlugin = await _db.Queryable<AiPlugin>()
                        .Where(item => item.TenantIdValue == tenantId.Value && item.Id == aiPluginId)
                        .FirstAsync(cancellationToken);
                    return aiPlugin is null
                        ? reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing")
                        : reference.ToResolved(
                            exists: true,
                            displayName: aiPlugin.Name,
                            resolvedVersion: aiPlugin.PublishedVersion > 0 ? $"v{aiPlugin.PublishedVersion}" : null,
                            status: aiPlugin.Status.ToString().ToLowerInvariant());
                }

                var lowCodePlugin = await _db.Queryable<LowCodePluginDefinition>()
                    .Where(item => item.TenantIdValue == tenantId.Value && item.PluginId == reference.ResourceId)
                    .FirstAsync(cancellationToken);
                return lowCodePlugin is null
                    ? reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing")
                    : reference.ToResolved(exists: true, displayName: lowCodePlugin.Name, resolvedVersion: lowCodePlugin.LatestVersion, status: "ready");
            }
            case "prompt-template":
            {
                if (!long.TryParse(reference.ResourceId, out var promptTemplateId))
                {
                    return reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "invalid-id");
                }

                var template = await _db.Queryable<AppPromptTemplate>()
                    .Where(item => item.TenantIdValue == tenantId.Value && item.Id == promptTemplateId)
                    .FirstAsync(cancellationToken);
                return template is null
                    ? reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing")
                    : reference.ToResolved(exists: true, displayName: template.Name, resolvedVersion: template.Version, status: "ready");
            }
            case "trigger":
            {
                var trigger = await _db.Queryable<LowCodeTrigger>()
                    .Where(item => item.TenantIdValue == tenantId.Value && item.TriggerId == reference.ResourceId)
                    .FirstAsync(cancellationToken);
                return trigger is null
                    ? reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing")
                    : reference.ToResolved(exists: true, displayName: trigger.Name, resolvedVersion: null, status: trigger.Enabled ? "enabled" : "disabled");
            }
            case "variable":
            {
                if (reference.ResourceId.StartsWith("page.", StringComparison.OrdinalIgnoreCase))
                {
                    var pageVariableCode = reference.ResourceId["page.".Length..];
                    var exists = !string.IsNullOrWhiteSpace(reference.PageId)
                        && pageVariableLookup.TryGetValue(reference.PageId, out var pageVariables)
                        && pageVariables.Contains(pageVariableCode);
                    return reference.ToResolved(exists, pageVariableCode, null, exists ? "ready" : "missing");
                }

                return variableLookup.TryGetValue(reference.ResourceId, out var variable)
                    ? reference.ToResolved(exists: true, displayName: variable.DisplayName, resolvedVersion: variable.UpdatedAt.ToString("yyyyMMddHHmmss"), status: "ready")
                    : reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "missing");
            }
            default:
                _logger.LogDebug("ProjectIdeDependencyGraphService encountered unsupported resource type {ResourceType}", reference.ResourceType);
                return reference.ToResolved(exists: false, displayName: null, resolvedVersion: null, status: "unsupported");
        }
    }

    private string BuildResourceSnapshotJson(
        TenantId tenantId,
        IReadOnlyList<AppVariable> variables,
        IReadOnlyList<ProjectIdeReferenceDto> references)
    {
        var snapshot = new JsonObject
        {
            ["workflowVersions"] = ToJsonArray(references.Where(reference => reference.ResourceType == "workflow")),
            ["chatflowVersions"] = ToJsonArray(references.Where(reference => reference.ResourceType == "chatflow")),
            ["databaseBindings"] = ToJsonArray(references.Where(reference => reference.ResourceType == "database")),
            ["knowledgeBindings"] = ToJsonArray(references.Where(reference => reference.ResourceType == "knowledge")),
            ["pluginVersions"] = ToJsonArray(references.Where(reference => reference.ResourceType == "plugin")),
            ["promptTemplateVersions"] = ToJsonArray(references.Where(reference => reference.ResourceType == "prompt-template")),
            ["triggerBindings"] = ToJsonArray(references.Where(reference => reference.ResourceType == "trigger")),
            ["variableSnapshot"] = new JsonArray(
                variables.Select(variable =>
                {
                    JsonNode? defaultValueNode;
                    JsonNode? validationNode;
                    try
                    {
                        defaultValueNode = JsonNode.Parse(variable.DefaultValueJson);
                    }
                    catch (JsonException)
                    {
                        defaultValueNode = variable.DefaultValueJson;
                    }

                    try
                    {
                        validationNode = string.IsNullOrWhiteSpace(variable.ValidationJson) ? null : JsonNode.Parse(variable.ValidationJson);
                    }
                    catch (JsonException)
                    {
                        validationNode = variable.ValidationJson;
                    }

                    return (JsonNode?)new JsonObject
                    {
                        ["id"] = variable.Id.ToString(),
                        ["code"] = variable.Code,
                        ["scope"] = variable.Scope,
                        ["valueType"] = variable.ValueType,
                        ["readonly"] = variable.IsReadOnly,
                        ["persist"] = variable.IsPersisted,
                        ["defaultValue"] = defaultValueNode,
                        ["validation"] = validationNode,
                        ["updatedAt"] = variable.UpdatedAt
                    };
                }).ToArray()),
            ["assetManifest"] = new JsonArray()
        };

        return snapshot.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static JsonArray ToJsonArray(IEnumerable<ProjectIdeReferenceDto> references)
    {
        return new JsonArray(references
            .DistinctBy(reference => $"{reference.ResourceType}:{reference.ResourceId}", StringComparer.OrdinalIgnoreCase)
            .Select(reference => (JsonNode?)new JsonObject
            {
                ["id"] = reference.ResourceId,
                ["name"] = reference.DisplayName,
                ["version"] = reference.ResolvedVersion,
                ["status"] = reference.Status
            }).ToArray());
    }

    private static bool JsonStringEquals(string left, string right)
    {
        try
        {
            using var leftDoc = JsonDocument.Parse(left);
            using var rightDoc = JsonDocument.Parse(right);
            return leftDoc.RootElement.ToString() == rightDoc.RootElement.ToString();
        }
        catch (JsonException)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }

    private static void Walk(JsonElement node, string currentPath, string? currentPageId, string? currentComponentId, List<ExtractedReference> references)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var pageId = currentPageId;
                var componentId = currentComponentId;
                if (node.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                {
                    if (currentPath.Contains("/pages", StringComparison.OrdinalIgnoreCase) && !currentPath.Contains("/root", StringComparison.OrdinalIgnoreCase))
                    {
                        pageId = idEl.GetString();
                    }

                    if (currentPath.Contains("/root", StringComparison.OrdinalIgnoreCase) || currentPath.Contains("/children", StringComparison.OrdinalIgnoreCase))
                    {
                        componentId = idEl.GetString();
                    }
                }

                foreach (var property in node.EnumerateObject())
                {
                    var childPath = $"{currentPath}/{property.Name}";

                    if (property.Value.ValueKind == JsonValueKind.String && ResourceFieldMapping.TryGetValue(property.Name, out var resourceType))
                    {
                        var resourceId = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(resourceId))
                        {
                            references.Add(new ExtractedReference(resourceType, resourceId!, childPath, pageId, componentId));
                        }
                    }
                    else if (string.Equals(property.Name, "path", StringComparison.OrdinalIgnoreCase)
                             && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var pathString = property.Value.GetString() ?? string.Empty;
                        var match = VariablePathRegex.Match(pathString);
                        if (match.Success)
                        {
                            references.Add(new ExtractedReference(
                                "variable",
                                $"{match.Groups[1].Value}.{match.Groups[2].Value}",
                                childPath,
                                pageId,
                                componentId));
                        }
                    }

                    Walk(property.Value, childPath, pageId, componentId, references);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                var index = 0;
                foreach (var item in node.EnumerateArray())
                {
                    Walk(item, $"{currentPath}[{index}]", currentPageId, currentComponentId, references);
                    index++;
                }

                break;
            }
        }
    }

    private sealed record ExtractedReference(
        string ResourceType,
        string ResourceId,
        string ReferencePath,
        string? PageId,
        string? ComponentId)
    {
        public ProjectIdeReferenceDto ToResolved(bool exists, string? displayName, string? resolvedVersion, string? status) =>
            new(
                ResourceType: ResourceType,
                ResourceId: ResourceId,
                DisplayName: displayName,
                ResolvedVersion: resolvedVersion,
                Status: status,
                Exists: exists,
                ReferencePath: ReferencePath,
                PageId: PageId,
                ComponentId: ComponentId);
    }
}
