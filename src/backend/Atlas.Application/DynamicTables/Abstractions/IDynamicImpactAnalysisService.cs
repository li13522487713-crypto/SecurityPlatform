using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IDynamicImpactAnalysisService
{
    Task<DynamicImpactAnalysisResult> AnalyzeAsync(
        TenantId tenantId,
        string tableKey,
        IReadOnlyList<string>? fieldNames,
        CancellationToken cancellationToken);
}
