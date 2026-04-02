namespace Atlas.Application.DynamicTables.Models;

/// <summary>
/// DDL 预览结果（T02-12 up script + T02-30 down hint + T02-13 warning list）
/// </summary>
public sealed record DdlPreviewResult(
    string TableKey,
    string UpScript,
    string? DownHint,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<DdlCapabilityWarning> CapabilityWarnings);

public sealed record DdlCapabilityWarning(
    string Feature,
    string DbType,
    string Description);

/// <summary>
/// 三阶段迁移任务模型（T02-14）
/// </summary>
public sealed record ExpandMigrateContractTask(
    long TaskId,
    string TableKey,
    string Phase,
    IReadOnlyList<string> ExpandScripts,
    IReadOnlyList<string> MigrateScripts,
    IReadOnlyList<string> ContractScripts,
    DateTimeOffset CreatedAt);

/// <summary>
/// 迁移阶段执行结果
/// </summary>
public sealed record MigrationPhaseResult(
    string Phase,
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<string> ExecutedScripts);

/// <summary>
/// 影响分析列表（T02-31 — 结构化影响对象列表）
/// </summary>
public sealed record SchemaImpactList(
    string TableKey,
    IReadOnlyList<SchemaImpactItem> ImpactedViews,
    IReadOnlyList<SchemaImpactItem> ImpactedFunctions,
    IReadOnlyList<SchemaImpactItem> ImpactedFlows,
    int TotalCount);

public sealed record SchemaImpactItem(
    string ResourceType,
    string ResourceId,
    string ResourceName,
    string ImpactDescription,
    string? NavigationPath);
