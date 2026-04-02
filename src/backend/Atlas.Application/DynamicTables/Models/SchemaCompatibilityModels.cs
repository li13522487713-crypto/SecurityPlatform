namespace Atlas.Application.DynamicTables.Models;

/// <summary>
/// 兼容性检查结果
/// </summary>
public sealed record SchemaCompatibilityResult(
    bool IsCompatible,
    IReadOnlyList<CompatibilityIssue> Issues,
    IReadOnlyList<HighRiskWarning> HighRiskWarnings);

public sealed record CompatibilityIssue(
    string Category,
    string Severity,
    string ObjectName,
    string Description,
    string? SuggestedAction);

public sealed record HighRiskWarning(
    string WarningType,
    string ObjectName,
    string Description,
    string RiskLevel);

/// <summary>
/// 兼容性检查请求
/// </summary>
public sealed record SchemaCompatibilityCheckRequest(
    string TableKey,
    IReadOnlyList<DynamicFieldDefinition>? AddFields,
    IReadOnlyList<DynamicFieldUpdateDefinition>? UpdateFields,
    IReadOnlyList<string>? RemoveFields,
    IReadOnlyList<DynamicIndexDefinition>? AddIndexes,
    IReadOnlyList<string>? RemoveIndexes);
