using Atlas.Core.Models;

namespace Atlas.Application.System.Models;

public sealed record AppMigrationTaskCreateRequest(
    string AppInstanceId,
    bool ReadOnlyWindow = true,
    bool EnableDualWrite = false,
    bool EnableRollback = true);

public sealed record AppMigrationTaskListItem(
    string Id,
    string TenantId,
    string AppInstanceId,
    string Status,
    string Phase,
    int TotalItems,
    int CompletedItems,
    int FailedItems,
    decimal ProgressPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? ErrorSummary);

public sealed record AppMigrationTaskDetail(
    string Id,
    string TenantId,
    string AppInstanceId,
    string DataSourceId,
    string Status,
    string Phase,
    int TotalItems,
    int CompletedItems,
    int FailedItems,
    decimal ProgressPercent,
    string? CurrentObjectName,
    int? CurrentBatchNo,
    bool ReadOnlyWindow,
    bool EnableDualWrite,
    bool EnableRollback,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? ErrorSummary);

public sealed record AppMigrationTaskProgress(
    string TaskId,
    string Status,
    string Phase,
    int TotalItems,
    int CompletedItems,
    int FailedItems,
    decimal ProgressPercent,
    string? CurrentObjectName,
    int? CurrentBatchNo,
    DateTimeOffset UpdatedAt,
    string? ErrorSummary);

public sealed record AppMigrationPrecheckResult(
    string TaskId,
    bool CanStart,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Warnings);

public sealed record AppIntegrityCheckSummary(
    string TaskId,
    bool Passed,
    int TotalChecks,
    int PassedChecks,
    int FailedChecks,
    DateTimeOffset CheckedAt);

public sealed record AppMigrationCutoverRequest(
    bool EnableReadOnlyWindow = true,
    bool EnableDualWrite = false);

public sealed record AppMigrationActionResult(
    bool Success,
    string TaskId,
    string Status,
    string? Message = null);

public sealed record AppMigrationBindingRepairRequest(
    string AppInstanceId);

public sealed record AppMigrationBindingRepairResult(
    string AppInstanceId,
    string DataSourceId,
    bool Repaired,
    string Message);
