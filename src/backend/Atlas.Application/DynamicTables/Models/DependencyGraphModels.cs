namespace Atlas.Application.DynamicTables.Models;

/// <summary>
/// 依赖图查询结果（T02-17 ~ T02-20）
/// </summary>
public sealed record DependencyGraphResult(
    string TableKey,
    IReadOnlyList<DependencyEdge> Dependencies,
    int TotalDependencyCount);

public sealed record DependencyEdge(
    string SourceType,
    string SourceKey,
    string TargetType,
    string TargetKey,
    string RelationDescription);

/// <summary>
/// 计算字段绑定模型（T02-21）
/// </summary>
public sealed record ComputedFieldBindingResult(
    string TableKey,
    string FieldName,
    long ExpressionId,
    object? ComputedValue,
    string? ErrorMessage);
