using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.WorkflowEngine;
using SqlSugar;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class DagWorkflowQueryService : IDagWorkflowQueryService
{
    private static readonly Regex TemplateVariableRegex = new("{{\\s*([^{}]+?)\\s*}}", RegexOptions.Compiled);
    private readonly IWorkflowMetaRepository _metaRepo;
    private readonly IWorkflowDraftRepository _draftRepo;
    private readonly IWorkflowVersionRepository _versionRepo;
    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly IWorkflowNodeExecutionRepository _nodeExecutionRepo;
    private readonly ISqlSugarClient _db;
    private readonly AiPluginRepository _pluginRepository;
    private readonly AiPluginApiRepository _pluginApiRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly NodeExecutorRegistry _registry;
    private readonly IAiVariableService? _variableService;

    public DagWorkflowQueryService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowVersionRepository versionRepo,
        IWorkflowExecutionRepository executionRepo,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        ISqlSugarClient db,
        AiPluginRepository pluginRepository,
        AiPluginApiRepository pluginApiRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        AiDatabaseRepository databaseRepository,
        NodeExecutorRegistry registry,
        IAiVariableService? variableService = null)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _versionRepo = versionRepo;
        _executionRepo = executionRepo;
        _nodeExecutionRepo = nodeExecutionRepo;
        _db = db;
        _pluginRepository = pluginRepository;
        _pluginApiRepository = pluginApiRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _databaseRepository = databaseRepository;
        _registry = registry;
        _variableService = variableService;
    }

    public async Task<PagedResult<DagWorkflowListItem>> ListAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await _metaRepo.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        var dtos = items.Select(MapListItem).ToList();
        return new PagedResult<DagWorkflowListItem>(dtos, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<DagWorkflowListItem>> ListPublishedAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await _metaRepo.GetPagedByStatusAsync(
            tenantId,
            WorkflowLifecycleStatus.Published,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        var dtos = items.Select(MapListItem).ToList();
        return new PagedResult<DagWorkflowListItem>(dtos, total, pageIndex, pageSize);
    }

    public async Task<DagWorkflowDetailDto?> GetAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken,
        string? source = null,
        long? versionId = null)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken);
        if (meta is null) return null;

        if (string.Equals(source, "published", StringComparison.OrdinalIgnoreCase) || versionId.HasValue)
        {
            WorkflowVersion? version = null;
            if (versionId.HasValue)
            {
                version = await _versionRepo.FindByIdAsync(tenantId, versionId.Value, cancellationToken);
                if (version is null || version.WorkflowId != meta.Id)
                {
                    return null;
                }
            }
            else
            {
                version = await _versionRepo.GetLatestAsync(tenantId, meta.Id, cancellationToken);
            }

            if (version is null)
            {
                return null;
            }

            return MapDetail(meta, version);
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken);
        return MapDetail(meta, draft);
    }

    public async Task<IReadOnlyList<DagWorkflowVersionDto>> ListVersionsAsync(
        TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        var versions = await _versionRepo.ListByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        return versions.Select(MapVersion).ToList();
    }

    public async Task<DagWorkflowExecutionDto?> GetExecutionProcessAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null) return null;

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        return MapExecution(execution, nodeExecutions);
    }

    public async Task<DagWorkflowExecutionCheckpointDto?> GetExecutionCheckpointAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        var lastNode = nodeExecutions
            .OrderByDescending(node => node.CompletedAt ?? node.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        return new DagWorkflowExecutionCheckpointDto(
            execution.Id,
            execution.WorkflowId,
            execution.Status,
            lastNode?.NodeKey,
            execution.StartedAt,
            execution.CompletedAt,
            execution.InputsJson,
            execution.OutputsJson,
            execution.ErrorMessage);
    }

    public async Task<DagWorkflowExecutionDebugViewDto?> GetExecutionDebugViewAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        var executionDto = MapExecution(execution, nodeExecutions);
        var focusNode = executionDto.NodeExecutions
            .Where(node => node.Status is ExecutionStatus.Failed or ExecutionStatus.Interrupted)
            .OrderByDescending(node => node.CompletedAt ?? node.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault()
            ?? executionDto.NodeExecutions
                .OrderByDescending(node => node.CompletedAt ?? node.StartedAt ?? DateTime.MinValue)
                .FirstOrDefault();
        var reason = focusNode is null
            ? "暂无节点执行记录。"
            : focusNode.Status is ExecutionStatus.Failed
                ? "聚焦失败节点，便于快速定位错误。"
                : focusNode.Status is ExecutionStatus.Interrupted
                    ? "聚焦中断节点，便于恢复执行。"
                    : "聚焦最近执行节点。";

        return new DagWorkflowExecutionDebugViewDto(executionDto, focusNode, reason);
    }

    public async Task<DagWorkflowNodeExecutionDto?> GetNodeExecutionDetailAsync(
        TenantId tenantId, long executionId, string nodeKey, CancellationToken cancellationToken)
    {
        var nodeExec = await _nodeExecutionRepo.FindByNodeKeyAsync(tenantId, executionId, nodeKey, cancellationToken);
        return nodeExec is null ? null : MapNodeExecution(nodeExec);
    }

    public Task<IReadOnlyList<DagWorkflowNodeTypeDto>> GetNodeTypesAsync(CancellationToken cancellationToken)
    {
        var types = _registry.GetAllTypes()
            .Select(m =>
            {
                var declaration = _registry.GetDeclaration(m.Type);
                return new DagWorkflowNodeTypeDto(
                    m.Key,
                    m.Name,
                    m.Category,
                    m.Description,
                    declaration?.Ports,
                    declaration?.ConfigSchemaJson,
                    declaration?.UiMeta);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<DagWorkflowNodeTypeDto>>(types);
    }

    public Task<IReadOnlyList<DagWorkflowNodeTemplateDto>> GetNodeTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = _registry.GetAllTypes()
            .Select(metadata =>
            {
                var defaultConfig = BuiltInWorkflowNodeDeclarations.GetDefaultConfig(metadata.Type);
                return new DagWorkflowNodeTemplateDto(
                    metadata.Key,
                    metadata.Name,
                    metadata.Category,
                    defaultConfig);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<DagWorkflowNodeTemplateDto>>(templates);
    }

    public Task<IReadOnlyList<DagWorkflowNodeTemplateDto>> SearchNodeTemplatesAsync(
        string? keyword,
        IReadOnlyList<string>? categories,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var allTypes = _registry.GetAllTypes();
        var normalizedKeyword = keyword?.Trim();
        var hasKeyword = !string.IsNullOrEmpty(normalizedKeyword);
        var categorySet = categories is { Count: > 0 }
            ? new HashSet<string>(
                categories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()),
                StringComparer.OrdinalIgnoreCase)
            : null;

        var filtered = allTypes.Where(metadata =>
        {
            if (categorySet is not null && !categorySet.Contains(metadata.Category))
            {
                return false;
            }

            if (!hasKeyword)
            {
                return true;
            }

            return ContainsIgnoreCase(metadata.Key, normalizedKeyword)
                || ContainsIgnoreCase(metadata.Name, normalizedKeyword)
                || ContainsIgnoreCase(metadata.Description, normalizedKeyword)
                || ContainsIgnoreCase(metadata.Category, normalizedKeyword);
        }).ToList();

        var safePageIndex = pageIndex <= 0 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? filtered.Count : pageSize;
        var skipped = Math.Max(0, (safePageIndex - 1) * safePageSize);
        var paged = filtered.Skip(skipped).Take(safePageSize).ToList();

        var results = paged.Select(metadata =>
        {
            var defaultConfig = BuiltInWorkflowNodeDeclarations.GetDefaultConfig(metadata.Type);
            return new DagWorkflowNodeTemplateDto(
                metadata.Key,
                metadata.Name,
                metadata.Category,
                defaultConfig);
        }).ToList();

        return Task.FromResult<IReadOnlyList<DagWorkflowNodeTemplateDto>>(results);
    }

    public async Task<WorkflowVariableTreeDto> GetVariableTreeAsync(
        TenantId tenantId,
        long workflowId,
        string? nodeKey,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken);
        if (meta is null)
        {
            return new WorkflowVariableTreeDto(workflowId.ToString(), nodeKey, Array.Empty<WorkflowVariableGroup>());
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        var canvasJson = draft?.CanvasJson;
        var canvas = string.IsNullOrWhiteSpace(canvasJson)
            ? null
            : DagExecutor.ParseCanvas(canvasJson);

        var groups = new List<WorkflowVariableGroup>();
        var systemGroup = await BuildSystemVariableGroupAsync(cancellationToken);
        if (systemGroup is not null)
        {
            groups.Add(systemGroup);
        }

        if (canvas is not null)
        {
            var globalGroup = BuildGlobalVariableGroup(canvas);
            if (globalGroup is not null)
            {
                groups.Add(globalGroup);
            }

            var nodeGroups = BuildUpstreamNodeGroups(canvas, nodeKey);
            groups.AddRange(nodeGroups);
        }

        return new WorkflowVariableTreeDto(workflowId.ToString(), nodeKey, groups);
    }

    public async Task<WorkflowNodeExecutionHistoryDto?> GetNodeExecuteHistoryAsync(
        TenantId tenantId,
        long workflowId,
        long? executionId,
        string nodeKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nodeKey))
        {
            return null;
        }

        WorkflowExecution? execution = null;
        if (executionId is { } id and > 0)
        {
            execution = await _executionRepo.FindByIdAsync(tenantId, id, cancellationToken);
            if (execution is null || execution.WorkflowId != workflowId)
            {
                return null;
            }
        }
        else
        {
            execution = await _db.Queryable<WorkflowExecution>()
                .Where(e => e.TenantIdValue == tenantId.Value && e.WorkflowId == workflowId)
                .OrderBy(e => e.StartedAt, OrderByType.Desc)
                .FirstAsync(cancellationToken);
            if (execution is null)
            {
                return null;
            }
        }

        var nodeExec = await _nodeExecutionRepo.FindByNodeKeyAsync(
            tenantId, execution.Id, nodeKey, cancellationToken);
        if (nodeExec is null)
        {
            return null;
        }

        var contextVariablesJson = await BuildExecutionContextSnapshotAsync(
            tenantId, execution, nodeExec, cancellationToken);

        return new WorkflowNodeExecutionHistoryDto(
            workflowId.ToString(),
            execution.Id.ToString(),
            nodeExec.NodeKey,
            nodeExec.NodeType.ToString(),
            nodeExec.Status,
            nodeExec.InputsJson,
            nodeExec.OutputsJson,
            contextVariablesJson,
            nodeExec.ErrorMessage,
            nodeExec.StartedAt,
            nodeExec.CompletedAt,
            nodeExec.DurationMs);
    }

    public async Task<WorkflowHistorySchemaDto?> GetHistorySchemaAsync(
        TenantId tenantId,
        long workflowId,
        string? commitId,
        long? executionId,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken);
        if (meta is null)
        {
            return null;
        }

        // 1) 优先用 executionId 反查版本号（兼容 trace 回放场景）。
        WorkflowVersion? version = null;
        if (executionId is { } execId and > 0)
        {
            var execution = await _executionRepo.FindByIdAsync(tenantId, execId, cancellationToken);
            if (execution is not null && execution.WorkflowId == workflowId && execution.VersionNumber > 0)
            {
                version = await _versionRepo.FindByWorkflowAndVersionNumberAsync(
                    tenantId, workflowId, execution.VersionNumber, cancellationToken);
            }
        }

        // 2) 再尝试 commitId（前端历史抽屉传入的版本号字符串）。
        if (version is null && !string.IsNullOrWhiteSpace(commitId))
        {
            if (long.TryParse(commitId, out var versionId) && versionId > 0)
            {
                version = await _versionRepo.FindByIdAsync(tenantId, versionId, cancellationToken);
                if (version is not null && version.WorkflowId != workflowId)
                {
                    version = null;
                }
            }

            if (version is null && int.TryParse(commitId, out var versionNumber) && versionNumber > 0)
            {
                version = await _versionRepo.FindByWorkflowAndVersionNumberAsync(
                    tenantId, workflowId, versionNumber, cancellationToken);
            }
        }

        // 3) 兜底使用最新已发布版本，未发布则回落到草稿。
        if (version is null)
        {
            version = await _versionRepo.GetLatestAsync(tenantId, workflowId, cancellationToken);
        }

        if (version is not null)
        {
            return new WorkflowHistorySchemaDto(
                workflowId.ToString(),
                commitId ?? version.Id.ToString(CultureInfo.InvariantCulture),
                version.CanvasJson,
                meta.Name,
                meta.Description,
                version.PublishedAt);
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        return new WorkflowHistorySchemaDto(
            workflowId.ToString(),
            commitId ?? draft.CommitId,
            string.IsNullOrWhiteSpace(draft.CanvasJson) ? "{\"nodes\":[],\"connections\":[]}" : draft.CanvasJson,
            meta.Name,
            meta.Description,
            draft.UpdatedAt);
    }

    private async Task<WorkflowVariableGroup?> BuildSystemVariableGroupAsync(CancellationToken cancellationToken)
    {
        if (_variableService is null)
        {
            return null;
        }

        var defs = await _variableService.GetSystemVariableDefinitionsAsync(cancellationToken);
        if (defs is null || defs.Count == 0)
        {
            return null;
        }

        var fields = defs
            .OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase)
            .Select(d => new WorkflowVariableField(
                d.Key,
                string.IsNullOrWhiteSpace(d.Name) ? d.Key : d.Name,
                "string",
                d.Description,
                false,
                d.DefaultValue,
                null))
            .ToArray();

        return new WorkflowVariableGroup(
            WorkflowVariableScopeKind.System,
            "system",
            "系统变量",
            null,
            null,
            fields,
            "由平台提供的全局可用变量。");
    }

    private static WorkflowVariableGroup? BuildGlobalVariableGroup(Domain.AiPlatform.ValueObjects.CanvasSchema canvas)
    {
        if (canvas.Globals is null || canvas.Globals.Count == 0)
        {
            return null;
        }

        var fields = canvas.Globals
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new WorkflowVariableField(
                kv.Key,
                kv.Key,
                InferDataType(kv.Value),
                null,
                false,
                VariableResolver.ToDisplayText(kv.Value),
                null))
            .ToArray();

        return new WorkflowVariableGroup(
            WorkflowVariableScopeKind.Global,
            "global",
            "全局变量",
            null,
            null,
            fields,
            "画布级全局变量（CanvasSchema.Globals）。");
    }

    private static IReadOnlyList<WorkflowVariableGroup> BuildUpstreamNodeGroups(
        Domain.AiPlatform.ValueObjects.CanvasSchema canvas,
        string? nodeKey)
    {
        if (canvas.Nodes.Count == 0)
        {
            return Array.Empty<WorkflowVariableGroup>();
        }

        IReadOnlyCollection<Domain.AiPlatform.ValueObjects.NodeSchema> upstream;
        if (string.IsNullOrWhiteSpace(nodeKey))
        {
            upstream = canvas.Nodes;
        }
        else
        {
            upstream = ResolveUpstreamNodes(canvas, nodeKey);
        }

        return upstream
            .Where(node => node.Type != WorkflowNodeType.Comment)
            .OrderBy(node => node.Key, StringComparer.OrdinalIgnoreCase)
            .Select(node => new WorkflowVariableGroup(
                WorkflowVariableScopeKind.Node,
                node.Key,
                string.IsNullOrWhiteSpace(node.Label) ? node.Key : node.Label,
                node.Key,
                node.Type.ToString(),
                BuildNodeOutputFields(node),
                $"节点 {node.Type} 的输出参数。"))
            .ToArray();
    }

    private static IReadOnlyCollection<Domain.AiPlatform.ValueObjects.NodeSchema> ResolveUpstreamNodes(
        Domain.AiPlatform.ValueObjects.CanvasSchema canvas,
        string nodeKey)
    {
        var nodeMap = canvas.Nodes.ToDictionary(node => node.Key, StringComparer.OrdinalIgnoreCase);
        if (!nodeMap.ContainsKey(nodeKey))
        {
            return Array.Empty<Domain.AiPlatform.ValueObjects.NodeSchema>();
        }

        var predecessors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in canvas.Nodes)
        {
            predecessors[node.Key] = new List<string>();
        }

        foreach (var connection in canvas.Connections)
        {
            if (predecessors.TryGetValue(connection.TargetNodeKey, out var sources))
            {
                sources.Add(connection.SourceNodeKey);
            }
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        if (predecessors.TryGetValue(nodeKey, out var directSources))
        {
            foreach (var source in directSources)
            {
                queue.Enqueue(source);
            }
        }

        var collected = new List<Domain.AiPlatform.ValueObjects.NodeSchema>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (nodeMap.TryGetValue(current, out var node))
            {
                collected.Add(node);
            }

            if (predecessors.TryGetValue(current, out var nextSources))
            {
                foreach (var source in nextSources)
                {
                    if (!visited.Contains(source))
                    {
                        queue.Enqueue(source);
                    }
                }
            }
        }

        return collected;
    }

    private static IReadOnlyList<WorkflowVariableField> BuildNodeOutputFields(
        Domain.AiPlatform.ValueObjects.NodeSchema node)
    {
        var fields = new List<WorkflowVariableField>();
        if (node.OutputTypes is { Count: > 0 })
        {
            foreach (var (key, type) in node.OutputTypes.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                fields.Add(new WorkflowVariableField(
                    key,
                    key,
                    string.IsNullOrWhiteSpace(type) ? "string" : type,
                    null,
                    false,
                    null,
                    null));
            }

            return fields;
        }

        var declaration = BuiltInWorkflowNodeDeclarations.GetPorts(node.Type);
        var outputPorts = declaration
            .Where(p => p.Direction == WorkflowNodePortDirection.Output)
            .ToArray();
        if (outputPorts.Length == 0)
        {
            outputPorts = new[]
            {
                new WorkflowNodePortMetadata(
                    "output",
                    "output",
                    WorkflowNodePortDirection.Output,
                    "string")
            };
        }

        foreach (var port in outputPorts)
        {
            fields.Add(new WorkflowVariableField(
                port.Key,
                string.IsNullOrWhiteSpace(port.Name) ? port.Key : port.Name,
                string.IsNullOrWhiteSpace(port.DataType) ? "string" : port.DataType,
                null,
                port.IsRequired,
                null,
                null));
        }

        return fields;
    }

    private async Task<string?> BuildExecutionContextSnapshotAsync(
        TenantId tenantId,
        WorkflowExecution execution,
        WorkflowNodeExecution focusNode,
        CancellationToken cancellationToken)
    {
        // 聚合截至 focusNode 的执行上下文：开始节点输入 + 所有较早完成节点的输出。
        var siblings = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, execution.Id, cancellationToken);
        var ordered = siblings
            .Where(node => node.CompletedAt is not null
                && (focusNode.StartedAt is null
                    || node.CompletedAt <= focusNode.StartedAt))
            .OrderBy(node => node.CompletedAt)
            .ToArray();

        var snapshot = new SortedDictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var executionInputs = VariableResolver.ParseVariableDictionary(execution.InputsJson);
        foreach (var (key, value) in executionInputs)
        {
            snapshot["inputs." + key] = value;
        }

        foreach (var node in ordered)
        {
            var outputs = VariableResolver.ParseVariableDictionary(node.OutputsJson);
            foreach (var (key, value) in outputs)
            {
                snapshot[$"{node.NodeKey}.{key}"] = value;
            }
        }

        return snapshot.Count == 0
            ? null
            : JsonSerializer.Serialize(snapshot);
    }

    private static string InferDataType(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            _ => "string"
        };
    }

    private static bool ContainsIgnoreCase(string? source, string? keyword)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(keyword))
        {
            return false;
        }

        return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DagWorkflowRunTraceDto?> GetRunTraceAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null) return null;

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);

        var steps = nodeExecutions
            .OrderBy(n => n.StartedAt ?? DateTime.MaxValue)
            .Select(n => new DagWorkflowStepResultDto(
                executionId.ToString(),
                n.NodeKey,
                n.NodeType,
                n.Status,
                n.StartedAt,
                n.CompletedAt,
                n.DurationMs,
                TryParseJsonDict(n.InputsJson),
                TryParseJsonDict(n.OutputsJson),
                n.ErrorMessage))
            .ToList();

        var durationMs = execution.CompletedAt.HasValue
            ? (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds
            : null as long?;
        var edgeStatuses = await ResolveEdgeStatusesAsync(tenantId, execution, nodeExecutions, cancellationToken);

        return new DagWorkflowRunTraceDto(
            executionId.ToString(),
            execution.WorkflowId,
            execution.Status,
            execution.StartedAt,
            execution.CompletedAt,
            durationMs,
            steps,
            edgeStatuses);
    }

    public async Task<DagWorkflowDependencyDto?> GetDependenciesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken);
        if (meta is null)
        {
            return null;
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        if (draft is null || string.IsNullOrWhiteSpace(draft.CanvasJson))
        {
            return new DagWorkflowDependencyDto(
                workflowId.ToString(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>());
        }

        var canvas = DagExecutor.ParseCanvas(draft.CanvasJson);
        if (canvas is null)
        {
            return new DagWorkflowDependencyDto(
                workflowId.ToString(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>(),
                Array.Empty<DagWorkflowDependencyItemDto>());
        }
        var dependencies = new WorkflowDependencyAccumulator();

        foreach (var node in canvas.Nodes)
        {
            CollectDependencies(node, dependencies);
        }

        var subWorkflowIds = dependencies.SubWorkflowSources.Keys.ToArray();
        var pluginIds = dependencies.PluginSources.Keys.ToArray();
        var pluginApiIds = dependencies.PluginApiSources.Keys.ToArray();
        var knowledgeBaseIds = dependencies.KnowledgeBaseSources.Keys.ToArray();
        var databaseIds = dependencies.DatabaseSources.Keys.ToArray();

        if (pluginApiIds.Length > 0)
        {
            var pluginApis = await _pluginApiRepository.GetByIdsAsync(tenantId, pluginApiIds, cancellationToken);
            foreach (var pluginApi in pluginApis)
            {
                if (dependencies.PluginApiSources.TryGetValue(pluginApi.Id, out var apiSources))
                {
                    foreach (var sourceNodeKey in apiSources)
                    {
                        dependencies.AddPlugin(pluginApi.PluginId, sourceNodeKey);
                    }
                }
            }

            pluginIds = dependencies.PluginSources.Keys.ToArray();
        }

        var subWorkflows = subWorkflowIds.Length == 0
            ? new List<WorkflowMeta>()
            : await _db.Queryable<WorkflowMeta>()
                .Where(item => item.TenantIdValue == tenantId.Value && !item.IsDeleted && SqlFunc.ContainsArray(subWorkflowIds, item.Id))
                .ToListAsync(cancellationToken);
        var plugins = await _pluginRepository.QueryByIdsAsync(tenantId, pluginIds, cancellationToken);
        var knowledgeBases = await _knowledgeBaseRepository.QueryByIdsAsync(tenantId, knowledgeBaseIds, cancellationToken);
        var databases = await _databaseRepository.QueryByIdsAsync(tenantId, databaseIds, cancellationToken);

        return new DagWorkflowDependencyDto(
            workflowId.ToString(),
            BuildEntityDependencies(
                "workflow",
                subWorkflowIds,
                subWorkflows.ToDictionary(item => item.Id),
                item => item.Name,
                item => item.Description,
                dependencies.SubWorkflowSources),
            BuildEntityDependencies(
                "plugin",
                pluginIds,
                plugins.ToDictionary(item => item.Id),
                item => item.Name,
                item => item.Description,
                dependencies.PluginSources),
            BuildEntityDependencies(
                "knowledge-base",
                knowledgeBaseIds,
                knowledgeBases.ToDictionary(item => item.Id),
                item => item.Name,
                item => item.Description,
                dependencies.KnowledgeBaseSources),
            BuildEntityDependencies(
                "database",
                databaseIds,
                databases.ToDictionary(item => item.Id),
                item => item.Name,
                item => item.Description,
                dependencies.DatabaseSources),
            dependencies.VariableSources
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => new DagWorkflowDependencyItemDto(
                    "variable",
                    item.Key,
                    item.Key,
                    item.Value.Count > 0 ? $"来源节点 {item.Value.Count} 个" : null,
                    item.Value.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray()))
                .ToArray(),
            dependencies.ConversationSources
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => new DagWorkflowDependencyItemDto(
                    "conversation",
                    item.Key,
                    item.Key,
                    item.Value.Count > 0 ? $"来源节点 {item.Value.Count} 个" : null,
                    item.Value.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray()))
                .ToArray());
    }

    private static Dictionary<string, JsonElement>? TryParseJsonDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.Clone();
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<DagWorkflowEdgeRuntimeStatusDto>> ResolveEdgeStatusesAsync(
        TenantId tenantId,
        WorkflowExecution execution,
        IReadOnlyList<WorkflowNodeExecution> nodeExecutions,
        CancellationToken cancellationToken)
    {
        var canvas = await ResolveExecutionCanvasAsync(tenantId, execution, cancellationToken);
        if (canvas is null || canvas.Connections.Count == 0)
        {
            return Array.Empty<DagWorkflowEdgeRuntimeStatusDto>();
        }

        var nodeExecutionMap = nodeExecutions
            .GroupBy(node => node.NodeKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.CompletedAt ?? item.StartedAt ?? DateTime.MinValue)
                    .First(),
                StringComparer.OrdinalIgnoreCase);
        var nodeMap = canvas.Nodes.ToDictionary(node => node.Key, StringComparer.OrdinalIgnoreCase);
        var statuses = new List<DagWorkflowEdgeRuntimeStatusDto>(canvas.Connections.Count);

        foreach (var connection in canvas.Connections)
        {
            nodeExecutionMap.TryGetValue(connection.SourceNodeKey, out var sourceExecution);
            nodeExecutionMap.TryGetValue(connection.TargetNodeKey, out var targetExecution);
            nodeMap.TryGetValue(connection.SourceNodeKey, out var sourceNode);

            var (status, reason) = ResolveEdgeStatus(
                execution.Status,
                connection,
                sourceNode,
                sourceExecution,
                targetExecution);
            statuses.Add(new DagWorkflowEdgeRuntimeStatusDto(
                connection.SourceNodeKey,
                connection.SourcePort,
                connection.TargetNodeKey,
                connection.TargetPort,
                status,
                reason));
        }

        return statuses;
    }

    private async Task<Domain.AiPlatform.ValueObjects.CanvasSchema?> ResolveExecutionCanvasAsync(
        TenantId tenantId,
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        string? canvasJson = null;
        if (execution.VersionNumber > 0)
        {
            var version = await _versionRepo.FindByWorkflowAndVersionNumberAsync(
                tenantId,
                execution.WorkflowId,
                execution.VersionNumber,
                cancellationToken);
            canvasJson = version?.CanvasJson;
        }

        if (string.IsNullOrWhiteSpace(canvasJson))
        {
            var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, execution.WorkflowId, cancellationToken);
            canvasJson = draft?.CanvasJson;
        }

        return string.IsNullOrWhiteSpace(canvasJson)
            ? null
            : DagExecutor.ParseCanvas(canvasJson);
    }

    private static (EdgeExecutionStatus Status, string? Reason) ResolveEdgeStatus(
        ExecutionStatus executionStatus,
        Domain.AiPlatform.ValueObjects.ConnectionSchema connection,
        Domain.AiPlatform.ValueObjects.NodeSchema? sourceNode,
        WorkflowNodeExecution? sourceExecution,
        WorkflowNodeExecution? targetExecution)
    {
        if (sourceExecution is null)
        {
            return (EdgeExecutionStatus.Idle, null);
        }

        if (sourceExecution.Status == ExecutionStatus.Completed &&
            sourceNode?.Type == WorkflowNodeType.Selector &&
            TryResolveSelectorSkipReason(connection, sourceExecution.OutputsJson, out var selectorReason))
        {
            return (EdgeExecutionStatus.Skipped, selectorReason);
        }

        return sourceExecution.Status switch
        {
            ExecutionStatus.Failed => (EdgeExecutionStatus.Failed, "source_failed"),
            ExecutionStatus.Blocked => (EdgeExecutionStatus.Failed, "source_blocked"),
            ExecutionStatus.Skipped => (EdgeExecutionStatus.Skipped, "source_skipped"),
            ExecutionStatus.Running => (EdgeExecutionStatus.Incomplete, "source_running"),
            ExecutionStatus.Completed => ResolveCompletedSourceEdgeStatus(executionStatus, targetExecution),
            _ => (EdgeExecutionStatus.Idle, null)
        };
    }

    private static (EdgeExecutionStatus Status, string? Reason) ResolveCompletedSourceEdgeStatus(
        ExecutionStatus executionStatus,
        WorkflowNodeExecution? targetExecution)
    {
        if (targetExecution is null)
        {
            return executionStatus switch
            {
                ExecutionStatus.Completed => (EdgeExecutionStatus.Success, null),
                ExecutionStatus.Running => (EdgeExecutionStatus.Incomplete, "target_pending"),
                ExecutionStatus.Failed or ExecutionStatus.Cancelled or ExecutionStatus.Interrupted => (EdgeExecutionStatus.Failed, "target_not_reached"),
                _ => (EdgeExecutionStatus.Idle, null)
            };
        }

        return targetExecution.Status switch
        {
            ExecutionStatus.Completed or ExecutionStatus.Running or ExecutionStatus.Interrupted => (EdgeExecutionStatus.Success, null),
            ExecutionStatus.Skipped => (EdgeExecutionStatus.Skipped, targetExecution.ErrorMessage),
            ExecutionStatus.Blocked => (EdgeExecutionStatus.Failed, targetExecution.ErrorMessage ?? "target_blocked"),
            ExecutionStatus.Failed => (EdgeExecutionStatus.Failed, targetExecution.ErrorMessage ?? "target_failed"),
            _ => (EdgeExecutionStatus.Idle, null)
        };
    }

    private static bool TryResolveSelectorSkipReason(
        Domain.AiPlatform.ValueObjects.ConnectionSchema connection,
        string? outputsJson,
        out string reason)
    {
        reason = string.Empty;
        var outputs = TryParseJsonDict(outputsJson);
        if (outputs is null)
        {
            return false;
        }

        var selectedBranch = ResolveSelectedSelectorBranch(outputs);
        if (selectedBranch == SelectorBranch.True && IsSelectorFalseConnection(connection))
        {
            reason = "selector_unselected_branch";
            return true;
        }

        if (selectedBranch == SelectorBranch.False && IsSelectorTrueConnection(connection))
        {
            reason = "selector_unselected_branch";
            return true;
        }

        return false;
    }

    private static SelectorBranch? ResolveSelectedSelectorBranch(IReadOnlyDictionary<string, JsonElement> outputs)
    {
        if (outputs.TryGetValue("selected_branch", out var selectedBranchRaw))
        {
            var selectedBranchText = VariableResolver.ToDisplayText(selectedBranchRaw);
            if (selectedBranchText.Contains("true", StringComparison.OrdinalIgnoreCase))
            {
                return SelectorBranch.True;
            }

            if (selectedBranchText.Contains("false", StringComparison.OrdinalIgnoreCase))
            {
                return SelectorBranch.False;
            }
        }

        if (outputs.TryGetValue("selector_result", out var selectorResultRaw) &&
            VariableResolver.TryGetBoolean(selectorResultRaw, out var boolResult))
        {
            return boolResult ? SelectorBranch.True : SelectorBranch.False;
        }

        return null;
    }

    private static IReadOnlyList<DagWorkflowDependencyItemDto> BuildEntityDependencies<T>(
        string resourceType,
        IEnumerable<long> requestedIds,
        IReadOnlyDictionary<long, T> entityMap,
        Func<T, string> nameSelector,
        Func<T, string?> descriptionSelector,
        IReadOnlyDictionary<long, HashSet<string>> sourceMap)
    {
        var result = new List<DagWorkflowDependencyItemDto>();
        foreach (var id in requestedIds.OrderBy(item => item))
        {
            var sourceNodeKeys = sourceMap.TryGetValue(id, out var sources)
                ? sources.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();
            if (entityMap.TryGetValue(id, out var entity))
            {
                var name = nameSelector(entity);
                var description = descriptionSelector(entity);
                if (sourceNodeKeys.Length > 0 && string.IsNullOrWhiteSpace(description))
                {
                    description = $"来源节点 {sourceNodeKeys.Length} 个";
                }

                result.Add(new DagWorkflowDependencyItemDto(resourceType, id.ToString(), name, description, sourceNodeKeys));
                continue;
            }

            result.Add(new DagWorkflowDependencyItemDto(
                resourceType,
                id.ToString(),
                $"{resourceType} #{id}",
                "依赖资源不存在或已删除。",
                sourceNodeKeys));
        }

        return result;
    }

    private static void CollectDependencies(
        Domain.AiPlatform.ValueObjects.NodeSchema node,
        WorkflowDependencyAccumulator dependencies)
    {
        if (node.Type is WorkflowNodeType.CreateConversation
            or WorkflowNodeType.ConversationList
            or WorkflowNodeType.ConversationUpdate
            or WorkflowNodeType.ConversationDelete
            or WorkflowNodeType.ConversationHistory
            or WorkflowNodeType.ClearConversationHistory
            or WorkflowNodeType.MessageList
            or WorkflowNodeType.CreateMessage
            or WorkflowNodeType.EditMessage
            or WorkflowNodeType.DeleteMessage)
        {
            dependencies.AddConversation(node.Key, node.Key);
        }

        foreach (var entry in node.Config)
        {
            CollectDependenciesFromElement(node.Key, entry.Key, entry.Value, dependencies);
        }

        if (node.ChildCanvas is null)
        {
            return;
        }

        foreach (var childNode in node.ChildCanvas.Nodes)
        {
            CollectDependencies(childNode, dependencies);
        }
    }

    private static void CollectDependenciesFromElement(
        string nodeKey,
        string key,
        JsonElement value,
        WorkflowDependencyAccumulator dependencies)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (key.Equals("assignments", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("inputMappings", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var property in value.EnumerateObject())
                    {
                        dependencies.AddVariable(property.Name, nodeKey);
                        CollectDependenciesFromElement(nodeKey, property.Name, property.Value, dependencies);
                    }
                    return;
                }

                foreach (var property in value.EnumerateObject())
                {
                    CollectDependenciesFromElement(nodeKey, property.Name, property.Value, dependencies);
                }
                return;
            case JsonValueKind.Array:
                if (key.Equals("knowledgeIds", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        AddLongDependency(item, dependencies.AddKnowledgeBase, nodeKey);
                    }
                    return;
                }

                if (key.Equals("variableKeys", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        AddStringDependency(item, dependencies.AddVariable, nodeKey);
                    }
                    return;
                }

                foreach (var item in value.EnumerateArray())
                {
                    CollectDependenciesFromElement(nodeKey, key, item, dependencies);
                }
                return;
            case JsonValueKind.Number:
                if (!value.TryGetInt64(out var numberValue) || numberValue <= 0)
                {
                    return;
                }

                if (key.Equals("workflowId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddSubWorkflow(numberValue, nodeKey);
                }
                else if (key.Equals("pluginId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddPlugin(numberValue, nodeKey);
                }
                else if (key.Equals("apiId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddPluginApi(numberValue, nodeKey);
                }
                else if (key.Equals("knowledgeId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddKnowledgeBase(numberValue, nodeKey);
                }
                else if (key.Equals("databaseInfoId", StringComparison.OrdinalIgnoreCase) || key.Equals("databaseId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddDatabase(numberValue, nodeKey);
                }
                else if (key.Equals("conversationId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddConversation(numberValue.ToString(), nodeKey);
                }
                else if (key.Equals("messageId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddConversation($"message:{numberValue}", nodeKey);
                }
                return;
            case JsonValueKind.String:
                var text = value.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                if (key.Equals("workflowId", StringComparison.OrdinalIgnoreCase) && long.TryParse(text, out var workflowId) && workflowId > 0)
                {
                    dependencies.AddSubWorkflow(workflowId, nodeKey);
                }
                else if (key.Equals("pluginId", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(text, out var pluginId) && pluginId > 0)
                {
                    dependencies.AddPlugin(pluginId, nodeKey);
                }
                else if (key.Equals("apiId", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(text, out var apiId) && apiId > 0)
                {
                    dependencies.AddPluginApi(apiId, nodeKey);
                }
                else if (key.Equals("knowledgeId", StringComparison.OrdinalIgnoreCase) && long.TryParse(text, out var knowledgeId) && knowledgeId > 0)
                {
                    dependencies.AddKnowledgeBase(knowledgeId, nodeKey);
                }
                else if ((key.Equals("databaseInfoId", StringComparison.OrdinalIgnoreCase) || key.Equals("databaseId", StringComparison.OrdinalIgnoreCase))
                    && long.TryParse(text, out var databaseId) && databaseId > 0)
                {
                    dependencies.AddDatabase(databaseId, nodeKey);
                }
                else if (key.Equals("conversationId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddConversation(text, nodeKey);
                }
                else if (key.Equals("messageId", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddConversation($"message:{text}", nodeKey);
                }

                if (key.Contains("variable", StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.AddVariable(text, nodeKey);
                }

                foreach (Match match in TemplateVariableRegex.Matches(text))
                {
                    var variableKey = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(variableKey))
                    {
                        dependencies.AddVariable(variableKey, nodeKey);
                    }
                }
                return;
        }
    }

    private static void AddLongDependency(JsonElement element, Action<long, string> addAction, string nodeKey)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var numericValue) && numericValue > 0)
        {
            addAction(numericValue, nodeKey);
        }
        else if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out var parsedValue) && parsedValue > 0)
        {
            addAction(parsedValue, nodeKey);
        }
    }

    private static void AddStringDependency(JsonElement element, Action<string, string> addAction, string nodeKey)
    {
        if (element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            addAction(element.GetString()!.Trim(), nodeKey);
        }
    }

    private sealed class WorkflowDependencyAccumulator
    {
        public Dictionary<long, HashSet<string>> SubWorkflowSources { get; } = new();
        public Dictionary<long, HashSet<string>> PluginSources { get; } = new();
        public Dictionary<long, HashSet<string>> PluginApiSources { get; } = new();
        public Dictionary<long, HashSet<string>> KnowledgeBaseSources { get; } = new();
        public Dictionary<long, HashSet<string>> DatabaseSources { get; } = new();
        public Dictionary<string, HashSet<string>> VariableSources { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> ConversationSources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void AddSubWorkflow(long id, string nodeKey) => AddNumeric(SubWorkflowSources, id, nodeKey);
        public void AddPlugin(long id, string nodeKey) => AddNumeric(PluginSources, id, nodeKey);
        public void AddPluginApi(long id, string nodeKey) => AddNumeric(PluginApiSources, id, nodeKey);
        public void AddKnowledgeBase(long id, string nodeKey) => AddNumeric(KnowledgeBaseSources, id, nodeKey);
        public void AddDatabase(long id, string nodeKey) => AddNumeric(DatabaseSources, id, nodeKey);
        public void AddVariable(string key, string nodeKey) => AddString(VariableSources, key, nodeKey);
        public void AddConversation(string key, string nodeKey) => AddString(ConversationSources, key, nodeKey);

        private static void AddNumeric(Dictionary<long, HashSet<string>> map, long id, string nodeKey)
        {
            if (id <= 0)
            {
                return;
            }

            if (!map.TryGetValue(id, out var sourceNodeKeys))
            {
                sourceNodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[id] = sourceNodeKeys;
            }

            sourceNodeKeys.Add(nodeKey);
        }

        private static void AddString(Dictionary<string, HashSet<string>> map, string value, string nodeKey)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = value.Trim();
            if (!map.TryGetValue(normalized, out var sourceNodeKeys))
            {
                sourceNodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[normalized] = sourceNodeKeys;
            }

            sourceNodeKeys.Add(nodeKey);
        }
    }


    private static bool IsSelectorTrueConnection(Domain.AiPlatform.ValueObjects.ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("selector_result", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Contains("selected_branch", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true_branch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("true", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("true", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("yes", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelectorFalseConnection(Domain.AiPlatform.ValueObjects.ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("selector_result", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Contains("selected_branch", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false_branch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("false", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("false", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("no", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("no", StringComparison.OrdinalIgnoreCase);
    }

    private enum SelectorBranch
    {
        True = 1,
        False = 2
    }

    public async Task<WorkflowVersionDiff?> GetVersionDiffAsync(
        TenantId tenantId,
        long workflowId,
        long fromVersionId,
        long toVersionId,
        CancellationToken cancellationToken)
    {
        var fromVersion = await _versionRepo.FindByIdAsync(tenantId, fromVersionId, cancellationToken);
        var toVersion = await _versionRepo.FindByIdAsync(tenantId, toVersionId, cancellationToken);

        if (fromVersion is null || toVersion is null)
        {
            return null;
        }

        if (fromVersion.WorkflowId != workflowId || toVersion.WorkflowId != workflowId)
        {
            return null;
        }

        var (addedNodes, removedNodes, modifiedNodes, addedConnections, removedConnections) =
            DiffCanvasJson(fromVersion.CanvasJson, toVersion.CanvasJson);

        return new WorkflowVersionDiff(
            workflowId,
            fromVersionId,
            fromVersion.VersionNumber,
            toVersionId,
            toVersion.VersionNumber,
            addedNodes,
            removedNodes,
            modifiedNodes,
            addedConnections,
            removedConnections,
            addedNodes.Count + removedNodes.Count + modifiedNodes.Count + addedConnections + removedConnections > 0);
    }

    private static (
        IReadOnlyList<string> AddedNodes,
        IReadOnlyList<string> RemovedNodes,
        IReadOnlyList<string> ModifiedNodes,
        int AddedConnectionCount,
        int RemovedConnectionCount) DiffCanvasJson(string fromJson, string toJson)
    {
        using var fromDoc = System.Text.Json.JsonDocument.Parse(fromJson);
        using var toDoc = System.Text.Json.JsonDocument.Parse(toJson);

        var fromNodes = ExtractNodes(fromDoc.RootElement);
        var toNodes = ExtractNodes(toDoc.RootElement);

        var fromConnections = ExtractConnectionCount(fromDoc.RootElement);
        var toConnections = ExtractConnectionCount(toDoc.RootElement);

        var addedNodes = toNodes.Keys.Except(fromNodes.Keys).ToList();
        var removedNodes = fromNodes.Keys.Except(toNodes.Keys).ToList();
        var modifiedNodes = fromNodes.Keys.Intersect(toNodes.Keys)
            .Where(key => fromNodes[key] != toNodes[key])
            .ToList();

        return (addedNodes, removedNodes, modifiedNodes,
            Math.Max(0, toConnections - fromConnections),
            Math.Max(0, fromConnections - toConnections));
    }

    private static Dictionary<string, string> ExtractNodes(System.Text.Json.JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("nodes", out var nodesEl) || nodesEl.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return result;
        }

        foreach (var node in nodesEl.EnumerateArray())
        {
            var key = node.TryGetProperty("key", out var keyProp) ? keyProp.GetString()
                : node.TryGetProperty("id", out var idProp) ? idProp.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = node.GetRawText();
            }
        }

        return result;
    }

    private static int ExtractConnectionCount(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("connections", out var connectionsEl)
            && connectionsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return connectionsEl.GetArrayLength();
        }

        return 0;
    }

    private static DagWorkflowListItem MapListItem(WorkflowMeta meta)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId, meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static DagWorkflowDetailDto MapDetail(WorkflowMeta meta, WorkflowDraft? draft)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId,
            draft?.CanvasJson ?? "{}",
            draft?.CommitId,
            meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static DagWorkflowDetailDto MapDetail(WorkflowMeta meta, WorkflowVersion version)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId,
            version.CanvasJson,
            null,
            meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static DagWorkflowVersionDto MapVersion(WorkflowVersion v)
        => new(v.Id, v.WorkflowId, v.VersionNumber, v.ChangeLog, v.CanvasJson, v.PublishedAt, v.PublishedByUserId);

    private static DagWorkflowExecutionDto MapExecution(WorkflowExecution exec, IReadOnlyList<WorkflowNodeExecution> nodes)
        => new(exec.Id, exec.WorkflowId, exec.VersionNumber, exec.Status,
            exec.InputsJson, exec.OutputsJson, exec.ErrorMessage,
            exec.StartedAt, exec.CompletedAt,
            nodes.Select(MapNodeExecution).ToList());

    private static DagWorkflowNodeExecutionDto MapNodeExecution(WorkflowNodeExecution n)
        => new(n.Id, n.ExecutionId, n.NodeKey, n.NodeType, n.Status,
            n.InputsJson, n.OutputsJson, n.ErrorMessage,
            n.StartedAt, n.CompletedAt, n.DurationMs);
}
