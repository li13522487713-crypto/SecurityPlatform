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

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class CozeWorkflowQueryService : ICozeWorkflowQueryService
{
    private readonly IWorkflowMetaRepository _metaRepo;
    private readonly IWorkflowDraftRepository _draftRepo;
    private readonly IWorkflowVersionRepository _versionRepo;
    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly IWorkflowNodeExecutionRepository _nodeExecutionRepo;
    private readonly ISqlSugarClient _db;
    private readonly NodeExecutorRegistry _registry;
    private readonly IDagWorkflowQueryService _dagWorkflowQueryService;

    public CozeWorkflowQueryService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowVersionRepository versionRepo,
        IWorkflowExecutionRepository executionRepo,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        ISqlSugarClient db,
        NodeExecutorRegistry registry,
        IDagWorkflowQueryService dagWorkflowQueryService)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _versionRepo = versionRepo;
        _executionRepo = executionRepo;
        _nodeExecutionRepo = nodeExecutionRepo;
        _db = db;
        _registry = registry;
        _dagWorkflowQueryService = dagWorkflowQueryService;
    }

    public async Task<PagedResult<CozeWorkflowListItem>> ListAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _metaRepo.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        return new PagedResult<CozeWorkflowListItem>(items.Select(MapListItem).ToList(), total, pageIndex, pageSize);
    }

    public async Task<PagedResult<CozeWorkflowListItem>> ListPublishedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _metaRepo.GetPagedByStatusAsync(
            tenantId,
            WorkflowLifecycleStatus.Published,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<CozeWorkflowListItem>(items.Select(MapListItem).ToList(), total, pageIndex, pageSize);
    }

    public async Task<CozeWorkflowDetailDto?> GetAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken,
        string? source = null,
        long? versionId = null)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken);
        if (meta is null)
        {
            return null;
        }

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

            return version is null ? null : MapDetail(meta, version);
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken);
        return MapDetail(meta, draft);
    }

    public async Task<IReadOnlyList<CozeWorkflowVersionDto>> ListVersionsAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken)
    {
        var versions = await _versionRepo.ListByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        return versions.Select(MapVersion).ToList();
    }

    public async Task<CozeWorkflowExecutionDto?> GetExecutionProcessAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        return MapExecution(execution, nodeExecutions);
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
                    declaration?.UiMeta,
                    declaration?.FormMetaJson);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<DagWorkflowNodeTypeDto>>(types);
    }

    public Task<IReadOnlyList<DagWorkflowNodeTemplateDto>> GetNodeTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = _registry.GetAllTypes()
            .Select(metadata => new DagWorkflowNodeTemplateDto(
                metadata.Key,
                metadata.Name,
                metadata.Category,
                BuiltInWorkflowNodeDeclarations.GetDefaultConfig(metadata.Type)))
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
            ? new HashSet<string>(categories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()), StringComparer.OrdinalIgnoreCase)
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

            return metadata.Key.Contains(normalizedKeyword!, StringComparison.OrdinalIgnoreCase)
                || metadata.Name.Contains(normalizedKeyword!, StringComparison.OrdinalIgnoreCase)
                || metadata.Description.Contains(normalizedKeyword!, StringComparison.OrdinalIgnoreCase)
                || metadata.Category.Contains(normalizedKeyword!, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        var safePageIndex = pageIndex <= 0 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? filtered.Count : pageSize;
        var paged = filtered.Skip(Math.Max(0, (safePageIndex - 1) * safePageSize)).Take(safePageSize).ToList();
        return Task.FromResult<IReadOnlyList<DagWorkflowNodeTemplateDto>>(paged
            .Select(metadata => new DagWorkflowNodeTemplateDto(
                metadata.Key,
                metadata.Name,
                metadata.Category,
                BuiltInWorkflowNodeDeclarations.GetDefaultConfig(metadata.Type)))
            .ToList());
    }

    public async Task<CozeWorkflowHistorySchemaDto?> GetHistorySchemaAsync(
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

        WorkflowVersion? version = null;
        if (executionId is { } execId and > 0)
        {
            var execution = await _executionRepo.FindByIdAsync(tenantId, execId, cancellationToken);
            if (execution is not null && execution.WorkflowId == workflowId && execution.VersionNumber > 0)
            {
                version = await _versionRepo.FindByWorkflowAndVersionNumberAsync(
                    tenantId,
                    workflowId,
                    execution.VersionNumber,
                    cancellationToken);
            }
        }

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
                version = await _versionRepo.FindByWorkflowAndVersionNumberAsync(tenantId, workflowId, versionNumber, cancellationToken);
            }
        }

        if (version is null)
        {
            version = await _versionRepo.GetLatestAsync(tenantId, workflowId, cancellationToken);
        }

        if (version is not null)
        {
            return new CozeWorkflowHistorySchemaDto(
                workflowId.ToString(),
                commitId ?? version.Id.ToString(CultureInfo.InvariantCulture),
                version.SchemaJson,
                meta.Name,
                meta.Description,
                version.PublishedAt);
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        return new CozeWorkflowHistorySchemaDto(
            workflowId.ToString(),
            commitId ?? draft.CommitId,
            draft.SchemaJson,
            meta.Name,
            meta.Description,
            draft.UpdatedAt);
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

        WorkflowExecution? execution;
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
                .Where(x => x.TenantIdValue == tenantId.Value && x.WorkflowId == workflowId)
                .OrderBy(x => x.StartedAt, OrderByType.Desc)
                .FirstAsync(cancellationToken);
            if (execution is null)
            {
                return null;
            }
        }

        var nodeExecution = await _nodeExecutionRepo.FindByNodeKeyAsync(tenantId, execution.Id, nodeKey, cancellationToken);
        if (nodeExecution is null)
        {
            return null;
        }

        return new WorkflowNodeExecutionHistoryDto(
            workflowId.ToString(),
            execution.Id.ToString(),
            nodeExecution.NodeKey,
            nodeExecution.NodeType.ToString(),
            nodeExecution.Status,
            nodeExecution.InputsJson,
            nodeExecution.OutputsJson,
            execution.OutputsJson,
            nodeExecution.ErrorMessage,
            nodeExecution.StartedAt,
            nodeExecution.CompletedAt,
            nodeExecution.DurationMs);
    }

    public async Task<DagWorkflowRunTraceDto?> GetRunTraceAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var nodes = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        return new DagWorkflowRunTraceDto(
            execution.Id.ToString(),
            execution.WorkflowId,
            execution.Status,
            execution.StartedAt,
            execution.CompletedAt,
            execution.CompletedAt.HasValue ? (long)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds : null,
            nodes.Select(MapStep).ToList(),
            Array.Empty<DagWorkflowEdgeRuntimeStatusDto>());
    }

    public Task<CozeWorkflowReferenceDto?> GetDependenciesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken)
    {
        return MapDependenciesAsync(tenantId, workflowId, cancellationToken);
    }

    private async Task<CozeWorkflowReferenceDto?> MapDependenciesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken)
    {
        var dependencies = await _dagWorkflowQueryService.GetDependenciesAsync(tenantId, workflowId, cancellationToken);
        if (dependencies is null)
        {
            return null;
        }

        var resolvedWorkflowId = long.TryParse(dependencies.WorkflowId, out var parsedWorkflowId)
            ? parsedWorkflowId
            : workflowId;

        return new CozeWorkflowReferenceDto(
            resolvedWorkflowId,
            dependencies.SubWorkflows,
            dependencies.Plugins,
            dependencies.KnowledgeBases,
            dependencies.Databases,
            dependencies.Variables,
            dependencies.Conversations);
    }

    private static CozeWorkflowListItem MapListItem(WorkflowMeta meta)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId, meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static CozeWorkflowDetailDto MapDetail(WorkflowMeta meta, WorkflowDraft? draft)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId, draft?.CanvasJson ?? "{}", draft?.CommitId,
            meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static CozeWorkflowDetailDto MapDetail(WorkflowMeta meta, WorkflowVersion version)
        => new(meta.Id, meta.Name, meta.Description, meta.Mode, meta.Status,
            meta.LatestVersionNumber, meta.CreatorId, version.CanvasJson, null,
            meta.CreatedAt, meta.UpdatedAt, meta.PublishedAt);

    private static CozeWorkflowVersionDto MapVersion(WorkflowVersion version)
        => new(version.Id, version.WorkflowId, version.VersionNumber, version.ChangeLog, version.CanvasJson, version.PublishedAt, version.PublishedByUserId);

    private static CozeWorkflowExecutionDto MapExecution(WorkflowExecution execution, IReadOnlyList<WorkflowNodeExecution> nodes)
        => new(execution.Id, execution.WorkflowId, execution.VersionNumber, execution.Status,
            execution.InputsJson, execution.OutputsJson, execution.ErrorMessage,
            execution.StartedAt, execution.CompletedAt,
            nodes.Select(MapNodeExecution).ToList());

    private static DagWorkflowNodeExecutionDto MapNodeExecution(WorkflowNodeExecution node)
        => new(node.Id, node.ExecutionId, node.NodeKey, node.NodeType, node.Status,
            node.InputsJson, node.OutputsJson, node.ErrorMessage, node.StartedAt, node.CompletedAt, node.DurationMs);

    private static DagWorkflowStepResultDto MapStep(WorkflowNodeExecution node)
        => new(
            node.ExecutionId.ToString(),
            node.NodeKey,
            node.NodeType,
            node.Status,
            node.StartedAt,
            node.CompletedAt,
            node.DurationMs,
            null,
            null,
            node.ErrorMessage);
}
