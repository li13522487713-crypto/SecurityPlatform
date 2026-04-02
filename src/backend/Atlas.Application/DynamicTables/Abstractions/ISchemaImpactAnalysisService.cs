using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// 结构化影响分析服务（T02-31） —— 区别于 IDynamicImpactAnalysisService（页面/表单级），
/// 本接口关注 Schema 变更对视图/函数/流程的结构化影响列表。
/// </summary>
public interface ISchemaImpactAnalysisService
{
    Task<SchemaImpactList> AnalyzeAsync(
        TenantId tenantId,
        string tableKey,
        IReadOnlyList<string>? removingFields,
        CancellationToken cancellationToken);
}
