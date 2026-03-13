using System.Text.Json.Nodes;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.WorkflowCore.DSL.Interface;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiWorkflowDesignService : IAiWorkflowDesignService
{
    private readonly AiWorkflowDefinitionRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly AiWorkflowDslBuilder _dslBuilder;
    private readonly IDefinitionLoader _definitionLoader;

    public AiWorkflowDesignService(
        AiWorkflowDefinitionRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        AiWorkflowDslBuilder dslBuilder,
        IDefinitionLoader definitionLoader)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _dslBuilder = dslBuilder;
        _definitionLoader = definitionLoader;
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

    public async Task PublishAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流定义不存在。", ErrorCodes.NotFound);
        entity.Publish();
        await _repository.UpdateAsync(entity, cancellationToken);
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
        var adjacency = ids.ToDictionary(x => x, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
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

                if (adjacency.ContainsKey(source) && ids.Contains(target))
                {
                    adjacency[source].Add(target);
                }
            }
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
}
