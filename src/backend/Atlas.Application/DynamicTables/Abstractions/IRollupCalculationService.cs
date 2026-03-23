using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// 汇总计算服务：按 RollupDefinitionsJson 聚合子记录，更新主记录的汇总字段。
/// </summary>
public interface IRollupCalculationService
{
    /// <summary>
    /// 重新计算指定主记录的所有汇总字段。
    /// </summary>
    Task RecalculateAsync(
        TenantId tenantId,
        string masterTableKey,
        long masterRecordId,
        CancellationToken ct = default);
}

/// <summary>
/// 汇总计算定义：描述一个聚合字段的计算规则。
/// </summary>
public sealed record RollupDefinition(
    /// <summary>主记录中要写入结果的字段名</summary>
    string TargetField,
    /// <summary>子表 TableKey（与 DynamicRelation.RelatedTableKey 对应）</summary>
    string ChildTableKey,
    /// <summary>子表中参与聚合的字段名</summary>
    string ChildField,
    /// <summary>聚合函数：SUM / COUNT / MIN / MAX / AVG</summary>
    string AggregateFunction,
    /// <summary>过滤条件（可选），格式为 "fieldName operator value"，多条件用 AND 分隔</summary>
    string? FilterExpression = null);
