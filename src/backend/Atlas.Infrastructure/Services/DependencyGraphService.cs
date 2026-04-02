using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.DynamicViews.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 依赖图服务实现（T02-17 ~ T02-20）。
/// T02-18: 表→视图依赖
/// T02-19: 表→函数依赖（占位 — 待 Track-03 T03-16）
/// T02-20: 表→流程依赖（占位 — 待 Track-05 T05-01）
/// </summary>
public sealed class DependencyGraphService : IDependencyGraphService
{
    private readonly IDynamicTableRepository _tableRepo;
    private readonly IDynamicViewRepository _viewRepo;
    private readonly IDynamicRelationRepository _relationRepo;

    public DependencyGraphService(
        IDynamicTableRepository tableRepo,
        IDynamicViewRepository viewRepo,
        IDynamicRelationRepository relationRepo)
    {
        _tableRepo = tableRepo;
        _viewRepo = viewRepo;
        _relationRepo = relationRepo;
    }

    public async Task<DependencyGraphResult> GetDependenciesAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepo.FindByKeyAsync(tenantId, tableKey, null, cancellationToken);
        if (table is null)
        {
            return new DependencyGraphResult(tableKey, Array.Empty<DependencyEdge>(), 0);
        }

        var edges = new List<DependencyEdge>();

        await AddViewDependenciesAsync(tenantId, tableKey, edges, cancellationToken);
        await AddRelationDependenciesAsync(tenantId, table.Id, tableKey, edges, cancellationToken);
        AddFunctionDependencies(tableKey, edges);
        AddFlowDependencies(tableKey, edges);

        return new DependencyGraphResult(tableKey, edges, edges.Count);
    }

    /// <summary>T02-18: 表→视图依赖</summary>
    private async Task AddViewDependenciesAsync(
        TenantId tenantId,
        string tableKey,
        List<DependencyEdge> edges,
        CancellationToken cancellationToken)
    {
        var views = await _viewRepo.FindByTableReferenceAsync(tenantId, null, tableKey, cancellationToken);
        foreach (var view in views)
        {
            edges.Add(new DependencyEdge(
                "Table", tableKey,
                "View", view.ViewKey,
                $"View '{view.ViewKey}' references table '{tableKey}' as data source"));
        }
    }

    private async Task AddRelationDependenciesAsync(
        TenantId tenantId,
        long tableId,
        string tableKey,
        List<DependencyEdge> edges,
        CancellationToken cancellationToken)
    {
        var relations = await _relationRepo.ListByTableIdAsync(tenantId, tableId, cancellationToken);
        foreach (var rel in relations)
        {
            edges.Add(new DependencyEdge(
                "Table", tableKey,
                "Table", rel.RelatedTableKey,
                $"Relation '{rel.SourceField}' → '{rel.RelatedTableKey}.{rel.TargetField}'"));
        }
    }

    /// <summary>T02-19: 表→函数依赖（占位 — 待 Track-03 FunctionDefinition 就绪后联调）</summary>
    private static void AddFunctionDependencies(string tableKey, List<DependencyEdge> edges)
    {
        // 占位：Track-03 T03-16 FunctionDefinition 实体就绪后，
        // 查询引用 tableKey 的函数定义并添加到依赖图
    }

    /// <summary>T02-20: 表→流程依赖（占位 — 待 Track-05 LogicFlowDefinition 就绪后联调）</summary>
    private static void AddFlowDependencies(string tableKey, List<DependencyEdge> edges)
    {
        // 占位：Track-05 T05-01 LogicFlowDefinition 实体就绪后，
        // 查询引用 tableKey 的逻辑流定义并添加到依赖图
    }
}
