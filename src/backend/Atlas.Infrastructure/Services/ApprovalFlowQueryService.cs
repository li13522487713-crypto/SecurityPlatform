using AutoMapper;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using System.Text.Json;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批流定义查询服务实现
/// </summary>
public sealed class ApprovalFlowQueryService : IApprovalFlowQueryService
{
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IMapper _mapper;

    public ApprovalFlowQueryService(IApprovalFlowRepository flowRepository, IMapper mapper)
    {
        _flowRepository = flowRepository;
        _mapper = mapper;
    }

    public async Task<ApprovalFlowDefinitionResponse?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _flowRepository.GetByIdAsync(tenantId, id, cancellationToken);
        return entity != null ? _mapper.Map<ApprovalFlowDefinitionResponse>(entity) : null;
    }

    public async Task<PagedResult<ApprovalFlowDefinitionListItem>> GetPagedAsync(
        TenantId tenantId,
        PagedRequest request,
        ApprovalFlowStatus? status = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _flowRepository.GetPagedAsync(
            tenantId,
            request.PageIndex,
            request.PageSize,
            status,
            keyword,
            cancellationToken);

        return new PagedResult<ApprovalFlowDefinitionListItem>(
            _mapper.Map<List<ApprovalFlowDefinitionListItem>>(items),
            totalCount,
            request.PageIndex,
            request.PageSize);
    }

    public async Task<ApprovalFlowExportResponse?> ExportAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _flowRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity == null)
        {
            return null;
        }

        return new ApprovalFlowExportResponse(
            entity.Id,
            entity.Name,
            entity.Version,
            entity.DefinitionJson,
            entity.Description,
            entity.Category,
            entity.VisibilityScopeJson,
            entity.IsQuickEntry,
            DateTimeOffset.UtcNow);
    }

    public async Task<ApprovalFlowCompareResponse?> CompareAsync(
        TenantId tenantId,
        long id,
        int targetVersion,
        CancellationToken cancellationToken)
    {
        var source = await _flowRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (source == null)
        {
            return null;
        }

        var target = await _flowRepository.GetByNameAndVersionAsync(tenantId, source.Name, targetVersion, cancellationToken);
        if (target == null)
        {
            return null;
        }

        var differences = CompareDefinitionJson(source.DefinitionJson, target.DefinitionJson);
        var summary = differences.Count == 0
            ? "版本定义完全一致"
            : $"共发现 {differences.Count} 处差异";

        return new ApprovalFlowCompareResponse(
            source.Id,
            source.Version,
            target.Version,
            differences.Count == 0,
            summary,
            differences);
    }

    private static IReadOnlyList<ApprovalFlowDifferenceItem> CompareDefinitionJson(string sourceJson, string targetJson)
    {
        if (string.Equals(sourceJson, targetJson, StringComparison.Ordinal))
        {
            return Array.Empty<ApprovalFlowDifferenceItem>();
        }

        var differences = new List<ApprovalFlowDifferenceItem>();
        try
        {
            using var sourceDoc = JsonDocument.Parse(sourceJson);
            using var targetDoc = JsonDocument.Parse(targetJson);

            var sourceRoot = sourceDoc.RootElement;
            var targetRoot = targetDoc.RootElement;

            var sourceNodeCount = GetNodeCount(sourceRoot);
            var targetNodeCount = GetNodeCount(targetRoot);
            if (sourceNodeCount != targetNodeCount)
            {
                differences.Add(new ApprovalFlowDifferenceItem(
                    "nodes.count",
                    sourceNodeCount.ToString(),
                    targetNodeCount.ToString(),
                    "Changed"));
            }

            var sourceEdgeCount = GetEdgeCount(sourceRoot);
            var targetEdgeCount = GetEdgeCount(targetRoot);
            if (sourceEdgeCount != targetEdgeCount)
            {
                differences.Add(new ApprovalFlowDifferenceItem(
                    "edges.count",
                    sourceEdgeCount.ToString(),
                    targetEdgeCount.ToString(),
                    "Changed"));
            }

            if (differences.Count == 0)
            {
                differences.Add(new ApprovalFlowDifferenceItem(
                    "definitionJson",
                    "内容摘要不同",
                    "内容摘要不同",
                    "Changed"));
            }
        }
        catch
        {
            differences.Add(new ApprovalFlowDifferenceItem(
                "definitionJson",
                "解析失败",
                "解析失败",
                "Changed"));
        }

        return differences;
    }

    private static int GetNodeCount(JsonElement root)
    {
        if (root.TryGetProperty("nodes", out var nodesElement))
        {
            if (nodesElement.ValueKind == JsonValueKind.Array)
            {
                return nodesElement.GetArrayLength();
            }
            if (nodesElement.ValueKind == JsonValueKind.Object && nodesElement.TryGetProperty("rootNode", out _))
            {
                return 1;
            }
        }
        return 0;
    }

    private static int GetEdgeCount(JsonElement root)
    {
        if (root.TryGetProperty("edges", out var edgesElement) && edgesElement.ValueKind == JsonValueKind.Array)
        {
            return edgesElement.GetArrayLength();
        }
        return 0;
    }
}
