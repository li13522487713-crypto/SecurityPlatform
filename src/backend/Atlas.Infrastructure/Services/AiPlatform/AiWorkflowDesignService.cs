using System.Text.Json.Nodes;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.WorkflowCore.DSL.Interface;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiWorkflowDesignService : IAiWorkflowDesignService
{
    private readonly AiWorkflowDefinitionRepository _repository;
    private readonly AiWorkflowSnapshotRepository _snapshotRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly AiWorkflowDslBuilder _dslBuilder;
    private readonly IDefinitionLoader _definitionLoader;
    private readonly IAuditWriter _auditWriter;

    public AiWorkflowDesignService(
        AiWorkflowDefinitionRepository repository,
        AiWorkflowSnapshotRepository snapshotRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        AiWorkflowDslBuilder dslBuilder,
        IDefinitionLoader definitionLoader,
        IAuditWriter auditWriter)
    {
        _repository = repository;
        _snapshotRepository = snapshotRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _dslBuilder = dslBuilder;
        _definitionLoader = definitionLoader;
        _auditWriter = auditWriter;
    }

    public async Task<PagedResult<AiWorkflowDefinitionDto>> ListAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        return new PagedResult<AiWorkflowDefinitionDto>(items.Select(MapList).ToList(), total, pageIndex, pageSize);
    }

    public async Task<AiWorkflowDetailDto?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long creatorId,
        AiWorkflowCreateRequest request,
        CancellationToken cancellationToken)
    {
        var id = _idGeneratorAccessor.NextId();
        var definitionJson = _dslBuilder.BuildDefinitionJson($"aiwf-{id}", 1, request.CanvasJson);
        var entity = new AiWorkflowDefinition(
            tenantId,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.CanvasJson,
            definitionJson,
            creatorId,
            id);
        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task SaveAsync(
        TenantId tenantId,
        long id,
        AiWorkflowSaveRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);
        var version = Math.Max(1, entity.PublishVersion + 1);
        var definitionJson = _dslBuilder.BuildDefinitionJson($"aiwf-{id}", version, request.CanvasJson);
        entity.Save(request.CanvasJson, definitionJson);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UpdateMetaAsync(
        TenantId tenantId,
        long id,
        AiWorkflowMetaUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);
        entity.UpdateMeta(request.Name.Trim(), request.Description?.Trim());
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _repository.DeleteAsync(tenantId, id, cancellationToken);
    }

    public async Task<long> CopyAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);
        var newId = _idGeneratorAccessor.NextId();
        var copied = new AiWorkflowDefinition(
            tenantId,
            $"{entity.Name}-副本",
            entity.Description,
            entity.CanvasJson,
            _dslBuilder.BuildDefinitionJson($"aiwf-{newId}", Math.Max(1, entity.PublishVersion + 1), entity.CanvasJson),
            creatorId,
            newId);
        await _repository.AddAsync(copied, cancellationToken);
        return newId;
    }

    public async Task PublishAsync(TenantId tenantId, long publisherId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);
        var validation = await ValidateAsync(tenantId, id, cancellationToken);
        if (!validation.IsValid)
        {
            throw new BusinessException(
                $"工作流校验失败，无法发布: {string.Join("; ", validation.Errors)}",
                ErrorCodes.ValidationError);
        }

        entity.Publish();
        await _repository.UpdateAsync(entity, cancellationToken);

        var snapshotId = _idGeneratorAccessor.NextId();
        var snapshot = new AiWorkflowSnapshot(
            tenantId,
            entity.Id,
            entity.PublishVersion,
            entity.DefinitionJson,
            entity.CanvasJson,
            entity.Name,
            publisherId,
            snapshotId);
        await _snapshotRepository.AddAsync(snapshot, cancellationToken);
    }

    public async Task<AiWorkflowValidateResult> ValidateAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);

        var errors = new List<string>();
        try
        {
            _ = _definitionLoader.LoadDefinitionFromJson(entity.DefinitionJson);
        }
        catch (Exception ex)
        {
            errors.Add($"DSL 解析失败: {ex.Message}");
        }

        try
        {
            var nodeError = ValidateCanvasGraph(entity.CanvasJson);
            if (!string.IsNullOrWhiteSpace(nodeError))
            {
                errors.Add(nodeError);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"画布校验失败: {ex.Message}");
        }

        return new AiWorkflowValidateResult(errors.Count == 0, errors);
    }

    private static string? ValidateCanvasGraph(string canvasJson)
    {
        if (string.IsNullOrWhiteSpace(canvasJson))
        {
            return "画布为空。";
        }

        var root = JsonNode.Parse(canvasJson);
        var nodes = root?["nodes"]?.AsArray();
        var edges = root?["edges"]?.AsArray();
        if (nodes is null || nodes.Count == 0)
        {
            return "至少需要一个节点。";
        }

        var ids = nodes
            .Select(x => x?["id"]?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (ids.Count == 0)
        {
            return "节点 ID 无效。";
        }

        var adjacency = ids.ToDictionary(x => x, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var inDegree = ids.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        if (edges is not null)
        {
            foreach (var edge in edges)
            {
                var source = edge?["source"]?.GetValue<string>();
                var target = edge?["target"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                if (!ids.Contains(source) || !ids.Contains(target))
                {
                    return $"连线包含不存在的节点: {source} -> {target}";
                }

                adjacency[source].Add(target);
                inDegree[target]++;
                if (adjacency[source].Count > 1)
                {
                    return $"节点 {source} 存在多个出边，当前版本仅支持单分支流转。";
                }
            }
        }

        var entryCount = inDegree.Values.Count(x => x == 0);
        if (entryCount == 0)
        {
            return "流程图缺少入口节点。";
        }

        var color = ids.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            if (HasCycle(id, adjacency, color))
            {
                return "流程图包含循环，请使用循环节点而非直接闭环连线。";
            }
        }

        return null;
    }

    private static bool HasCycle(
        string node,
        IReadOnlyDictionary<string, List<string>> adjacency,
        Dictionary<string, int> color)
    {
        if (color[node] == 1)
        {
            return true;
        }

        if (color[node] == 2)
        {
            return false;
        }

        color[node] = 1;
        if (adjacency.TryGetValue(node, out var next))
        {
            foreach (var target in next)
            {
                if (HasCycle(target, adjacency, color))
                {
                    return true;
                }
            }
        }

        color[node] = 2;
        return false;
    }

    private static AiWorkflowDefinitionDto MapList(AiWorkflowDefinition entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Status,
            entity.PublishVersion,
            entity.CreatorId,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.PublishedAt);

    private static AiWorkflowDetailDto MapDetail(AiWorkflowDefinition entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.CanvasJson,
            entity.DefinitionJson,
            entity.Status,
            entity.PublishVersion,
            entity.CreatorId,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.PublishedAt);

    public async Task<IReadOnlyList<AiWorkflowVersionItem>> GetVersionsAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        _ = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);

        var snapshots = await _snapshotRepository.GetByWorkflowIdAsync(tenantId, id, cancellationToken);
        return snapshots.Select(s => new AiWorkflowVersionItem(
            s.Id,
            s.Version,
            s.WorkflowName,
            s.PublishedByUserId,
            s.PublishedAt,
            s.ChangeLog)).ToList();
    }

    public async Task<AiWorkflowVersionDiff?> GetVersionDiffAsync(
        TenantId tenantId,
        long id,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken)
    {
        _ = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);

        var fromSnapshot = await _snapshotRepository.GetByVersionAsync(tenantId, id, fromVersion, cancellationToken);
        var toSnapshot = await _snapshotRepository.GetByVersionAsync(tenantId, id, toVersion, cancellationToken);

        if (fromSnapshot is null || toSnapshot is null)
        {
            return null;
        }

        var fromNodes = ExtractNodeIds(fromSnapshot.CanvasJson);
        var toNodes = ExtractNodeIds(toSnapshot.CanvasJson);
        var fromEdgeCount = CountEdges(fromSnapshot.CanvasJson);
        var toEdgeCount = CountEdges(toSnapshot.CanvasJson);

        var added = toNodes.Except(fromNodes).ToList();
        var removed = fromNodes.Except(toNodes).ToList();
        var common = fromNodes.Intersect(toNodes).ToList();

        var fromNodeMap = ExtractNodeMap(fromSnapshot.CanvasJson);
        var toNodeMap = ExtractNodeMap(toSnapshot.CanvasJson);
        var modified = common
            .Where(nodeId => fromNodeMap.TryGetValue(nodeId, out var fNode)
                             && toNodeMap.TryGetValue(nodeId, out var tNode)
                             && fNode != tNode)
            .ToList();

        return new AiWorkflowVersionDiff(
            id,
            fromVersion,
            toVersion,
            added,
            removed,
            modified,
            Math.Max(0, toEdgeCount - fromEdgeCount),
            Math.Max(0, fromEdgeCount - toEdgeCount));
    }

    public async Task<AiWorkflowRollbackResult> RollbackAsync(
        TenantId tenantId,
        long userId,
        long id,
        int targetVersion,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);

        var targetSnapshot = await _snapshotRepository.GetByVersionAsync(tenantId, id, targetVersion, cancellationToken)
            ?? throw new BusinessException($"版本 {targetVersion} 不存在。", ErrorCodes.NotFound);

        var previousVersion = entity.PublishVersion;
        entity.Save(targetSnapshot.CanvasJson, targetSnapshot.DefinitionJson);
        entity.Publish();
        await _repository.UpdateAsync(entity, cancellationToken);

        var snapshotId = _idGeneratorAccessor.NextId();
        var newSnapshot = new AiWorkflowSnapshot(
            tenantId,
            entity.Id,
            entity.PublishVersion,
            entity.DefinitionJson,
            entity.CanvasJson,
            entity.Name,
            userId,
            snapshotId);
        await _snapshotRepository.AddAsync(newSnapshot, cancellationToken);

        await _auditWriter.WriteAsync(new AuditRecord(
            tenantId,
            userId.ToString(),
            "AiWorkflow.Rollback",
            "Success",
            $"workflowId={id};fromVersion={previousVersion};targetVersion={targetVersion};newVersion={entity.PublishVersion}",
            null,
            null), cancellationToken);

        return new AiWorkflowRollbackResult(id, entity.PublishVersion, previousVersion);
    }

    private static HashSet<string> ExtractNodeIds(string canvasJson)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = JsonNode.Parse(canvasJson);
            var nodes = root?["nodes"]?.AsArray();
            if (nodes is null)
            {
                return result;
            }

            foreach (var node in nodes)
            {
                var nodeId = node?["id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    result.Add(nodeId);
                }
            }
        }
        catch
        {
            // canvas json invalid — return empty set
        }

        return result;
    }

    private static Dictionary<string, string> ExtractNodeMap(string canvasJson)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = JsonNode.Parse(canvasJson);
            var nodes = root?["nodes"]?.AsArray();
            if (nodes is null)
            {
                return result;
            }

            foreach (var node in nodes)
            {
                var nodeId = node?["id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    result[nodeId] = node?.ToJsonString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // canvas json invalid — return empty map
        }

        return result;
    }

    private static int CountEdges(string canvasJson)
    {
        try
        {
            var root = JsonNode.Parse(canvasJson);
            return root?["edges"]?.AsArray()?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
