using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.DynamicViews.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 结构化影响分析（T02-31）—— 列出表变更对视图/函数/流程的影响。
/// 函数和逻辑流引用为占位实现，待 Track-03 / Track-05 联调。
/// </summary>
public sealed class SchemaImpactAnalysisService : ISchemaImpactAnalysisService
{
    private readonly IDynamicTableRepository _tableRepo;
    private readonly IDynamicViewRepository _viewRepo;

    public SchemaImpactAnalysisService(
        IDynamicTableRepository tableRepo,
        IDynamicViewRepository viewRepo)
    {
        _tableRepo = tableRepo;
        _viewRepo = viewRepo;
    }

    public async Task<SchemaImpactList> AnalyzeAsync(
        TenantId tenantId,
        string tableKey,
        IReadOnlyList<string>? removingFields,
        CancellationToken cancellationToken)
    {
        var impactedViews = await FindImpactedViewsAsync(tenantId, tableKey, cancellationToken);
        var impactedFunctions = FindImpactedFunctions(tableKey, removingFields);
        var impactedFlows = FindImpactedFlows(tableKey, removingFields);

        return new SchemaImpactList(
            tableKey,
            impactedViews,
            impactedFunctions,
            impactedFlows,
            impactedViews.Count + impactedFunctions.Count + impactedFlows.Count);
    }

    private async Task<IReadOnlyList<SchemaImpactItem>> FindImpactedViewsAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var views = await _viewRepo.FindByTableReferenceAsync(tenantId, null, tableKey, cancellationToken);

        return views.Select(v => new SchemaImpactItem(
            "View",
            v.Id.ToString(),
            v.ViewKey,
            $"View references table '{tableKey}' as data source",
            $"/dynamic-views/{v.ViewKey}")).ToList();
    }

    /// <summary>占位 — 待 Track-03 FunctionDefinition 实体就绪后通过查询 computedExprId 关联</summary>
    private static IReadOnlyList<SchemaImpactItem> FindImpactedFunctions(
        string tableKey,
        IReadOnlyList<string>? removingFields)
    {
        return Array.Empty<SchemaImpactItem>();
    }

    /// <summary>占位 — 待 Track-05 LogicFlowDefinition 实体就绪后查询引用</summary>
    private static IReadOnlyList<SchemaImpactItem> FindImpactedFlows(
        string tableKey,
        IReadOnlyList<string>? removingFields)
    {
        return Array.Empty<SchemaImpactItem>();
    }
}
