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

    public async Task<WorkflowV2DetailDto?> GetAsync(
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
                var defaultConfig = BuiltInWorkflowNodeDeclarations.GetDefaultConfig(metadata.Type);
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
        var edgeStatuses = await ResolveEdgeStatusesAsync(tenantId, execution, nodeExecutions, cancellationToken);

        return new WorkflowV2RunTraceDto(
            executionId.ToString(),
            execution.WorkflowId,
            execution.Status,
            execution.StartedAt,
            execution.CompletedAt,
            durationMs,
            steps,
            edgeStatuses);
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

    private async Task<IReadOnlyList<WorkflowV2EdgeRuntimeStatusDto>> ResolveEdgeStatusesAsync(
        TenantId tenantId,
        WorkflowExecution execution,
        IReadOnlyList<WorkflowNodeExecution> nodeExecutions,
        CancellationToken cancellationToken)
    {
        var canvas = await ResolveExecutionCanvasAsync(tenantId, execution, cancellationToken);
        if (canvas is null || canvas.Connections.Count == 0)
        {
            return Array.Empty<WorkflowV2EdgeRuntimeStatusDto>();
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
        var statuses = new List<WorkflowV2EdgeRuntimeStatusDto>(canvas.Connections.Count);

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
            statuses.Add(new WorkflowV2EdgeRuntimeStatusDto(
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

    private static WorkflowV2ListItem MapListItem(WorkflowMeta meta)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId, meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static WorkflowV2DetailDto MapDetail(WorkflowMeta meta, WorkflowDraft? draft)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId,
            draft?.CanvasJson ?? "{}",
            draft?.CommitId,
            meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static WorkflowV2DetailDto MapDetail(WorkflowMeta meta, WorkflowVersion version)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId,
            version.CanvasJson,
            null,
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
