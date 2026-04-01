using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicImpactAnalysisService : IDynamicImpactAnalysisService
{
    private readonly ISqlSugarClient _db;

    public DynamicImpactAnalysisService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<DynamicImpactAnalysisResult> AnalyzeAsync(
        TenantId tenantId,
        string tableKey,
        IReadOnlyList<string>? fieldNames,
        CancellationToken cancellationToken)
    {
        // 批量查询，避免循环内执行数据库操作
        var pages = await _db.Queryable<LowCodePage>()
            .Where(p => p.TenantIdValue == tenantId.Value && p.DataTableKey == tableKey)
            .Select(p => new ImpactedResource("Page", p.Id.ToString(), p.Name, p.RoutePath))
            .ToListAsync(cancellationToken);

        var forms = await _db.Queryable<FormDefinition>()
            .Where(f => f.TenantIdValue == tenantId.Value && f.DataTableKey == tableKey)
            .Select(f => new ImpactedResource("Form", f.Id.ToString(), f.Name, null))
            .ToListAsync(cancellationToken);

        var totalCount = pages.Count + forms.Count;
        var riskLevel = totalCount > 10 ? "High"
            : totalCount > 3 ? "Medium"
            : "Low";

        return new DynamicImpactAnalysisResult(
            tableKey,
            pages,
            forms,
            Array.Empty<ImpactedResource>(),
            Array.Empty<ImpactedResource>(),
            riskLevel,
            totalCount);
    }
}
