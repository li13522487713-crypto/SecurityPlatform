using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class WorkflowV2QueryService : IWorkflowV2QueryService
{
    private readonly IWorkflowMetaRepository _metaRepo;
    private readonly IWorkflowDraftRepository _draftRepo;
    private readonly IWorkflowVersionRepository _versionRepo;
    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly IWorkflowNodeExecutionRepository _nodeExecutionRepo;
    private readonly NodeExecutorRegistry _registry;

    public WorkflowV2QueryService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowVersionRepository versionRepo,
        IWorkflowExecutionRepository executionRepo,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        NodeExecutorRegistry registry)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _versionRepo = versionRepo;
        _executionRepo = executionRepo;
        _nodeExecutionRepo = nodeExecutionRepo;
        _registry = registry;
    }

    public async Task<PagedResult<WorkflowV2ListItem>> ListAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await _metaRepo.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        var dtos = items.Select(MapListItem).ToList();
        return new PagedResult<WorkflowV2ListItem>(dtos, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<WorkflowV2ListItem>> ListPublishedAsync(
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
        return new PagedResult<WorkflowV2ListItem>(dtos, total, pageIndex, pageSize);
    }

    public async Task<WorkflowV2DetailDto?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken);
        if (meta is null) return null;

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken);
        return MapDetail(meta, draft);
    }

    public async Task<IReadOnlyList<WorkflowV2VersionDto>> ListVersionsAsync(
        TenantId tenantId, long workflowId, CancellationToken cancellationToken)
    {
        var versions = await _versionRepo.ListByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        return versions.Select(MapVersion).ToList();
    }

    public async Task<WorkflowV2ExecutionDto?> GetExecutionProcessAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null) return null;

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        return MapExecution(execution, nodeExecutions);
    }

    public async Task<WorkflowV2ExecutionCheckpointDto?> GetExecutionCheckpointAsync(
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

        return new WorkflowV2ExecutionCheckpointDto(
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

    public async Task<WorkflowV2ExecutionDebugViewDto?> GetExecutionDebugViewAsync(
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

        return new WorkflowV2ExecutionDebugViewDto(executionDto, focusNode, reason);
    }

    public async Task<WorkflowV2NodeExecutionDto?> GetNodeExecutionDetailAsync(
        TenantId tenantId, long executionId, string nodeKey, CancellationToken cancellationToken)
    {
        var nodeExec = await _nodeExecutionRepo.FindByNodeKeyAsync(tenantId, executionId, nodeKey, cancellationToken);
        return nodeExec is null ? null : MapNodeExecution(nodeExec);
    }

    public Task<IReadOnlyList<WorkflowV2NodeTypeDto>> GetNodeTypesAsync(CancellationToken cancellationToken)
    {
        var types = _registry.GetAllTypes()
            .Select(m =>
            {
                var declaration = _registry.GetDeclaration(m.Type);
                return new WorkflowV2NodeTypeDto(
                    m.Key,
                    m.Name,
                    m.Category,
                    m.Description,
                    declaration?.Ports,
                    declaration?.ConfigSchemaJson,
                    declaration?.UiMeta);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<WorkflowV2NodeTypeDto>>(types);
    }

    public Task<IReadOnlyList<WorkflowV2NodeTemplateDto>> GetNodeTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = _registry.GetAllTypes()
            .Select(metadata =>
            {
                var declaration = _registry.GetDeclaration(metadata.Type);
                var defaultConfig = declaration is null
                    ? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                    : ResolveDefaultConfig(declaration.ConfigSchemaJson);
                return new WorkflowV2NodeTemplateDto(
                    metadata.Key,
                    metadata.Name,
                    metadata.Category,
                    defaultConfig);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<WorkflowV2NodeTemplateDto>>(templates);
    }

    public async Task<WorkflowV2RunTraceDto?> GetRunTraceAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null) return null;

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);

        var steps = nodeExecutions
            .OrderBy(n => n.StartedAt ?? DateTime.MaxValue)
            .Select(n => new WorkflowV2StepResultDto(
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

        return new WorkflowV2RunTraceDto(
            executionId.ToString(),
            execution.WorkflowId,
            execution.Status,
            execution.StartedAt,
            execution.CompletedAt,
            durationMs,
            steps,
            EdgeStatuses: null);
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

    private static Dictionary<string, JsonElement> ResolveDefaultConfig(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            if (doc.RootElement.TryGetProperty("default", out var defaultConfig) &&
                defaultConfig.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in defaultConfig.EnumerateObject())
                {
                    result[p.Name] = p.Value.Clone();
                }

                return result;
            }
        }
        catch
        {
            // Ignore invalid config schema json.
        }

        return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
    }

    private static WorkflowV2ListItem MapListItem(WorkflowMeta meta)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId, meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static WorkflowV2DetailDto MapDetail(WorkflowMeta meta, WorkflowDraft? draft)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId,
            draft?.CanvasJson ?? "{}",
            draft?.CommitId,
            meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static WorkflowV2VersionDto MapVersion(WorkflowVersion v)
        => new(v.Id, v.WorkflowId, v.VersionNumber, v.ChangeLog, v.CanvasJson, v.PublishedAt, v.PublishedByUserId);

    private static WorkflowV2ExecutionDto MapExecution(WorkflowExecution exec, IReadOnlyList<WorkflowNodeExecution> nodes)
        => new(exec.Id, exec.WorkflowId, exec.VersionNumber, exec.Status,
            exec.InputsJson, exec.OutputsJson, exec.ErrorMessage,
            exec.StartedAt, exec.CompletedAt,
            nodes.Select(MapNodeExecution).ToList());

    private static WorkflowV2NodeExecutionDto MapNodeExecution(WorkflowNodeExecution n)
        => new(n.Id, n.ExecutionId, n.NodeKey, n.NodeType, n.Status,
            n.InputsJson, n.OutputsJson, n.ErrorMessage,
            n.StartedAt, n.CompletedAt, n.DurationMs);
}
