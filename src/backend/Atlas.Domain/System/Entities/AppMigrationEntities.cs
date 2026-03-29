using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.System.Entities;

public static class AppMigrationTaskStatuses
{
    public const string Pending = "Pending";
    public const string Prechecking = "Prechecking";
    public const string Ready = "Ready";
    public const string Running = "Running";
    public const string Validating = "Validating";
    public const string CutoverReady = "CutoverReady";
    public const string CutoverCompleted = "CutoverCompleted";
    public const string Failed = "Failed";
    public const string RolledBack = "RolledBack";
}

public sealed class AppMigrationTask : TenantEntity
{
    public AppMigrationTask()
        : base(TenantId.Empty)
    {
        Status = AppMigrationTaskStatuses.Pending;
        Phase = "Created";
    }

    public AppMigrationTask(
        TenantId tenantId,
        long tenantAppInstanceId,
        long dataSourceId,
        bool readOnlyWindow,
        bool enableDualWrite,
        bool enableRollback,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TenantAppInstanceId = tenantAppInstanceId;
        DataSourceId = dataSourceId;
        ReadOnlyWindow = readOnlyWindow;
        EnableDualWrite = enableDualWrite;
        EnableRollback = enableRollback;
        Status = AppMigrationTaskStatuses.Pending;
        Phase = "Created";
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedBy = createdBy;
        UpdatedAt = now;
    }

    public long TenantAppInstanceId { get; private set; }
    public long DataSourceId { get; private set; }
    public string Status { get; private set; }
    public string Phase { get; private set; }
    public int TotalItems { get; private set; }
    public int CompletedItems { get; private set; }
    public int FailedItems { get; private set; }
    public decimal ProgressPercent { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? CurrentObjectName { get; private set; }
    [SugarColumn(IsNullable = true)]
    public int? CurrentBatchNo { get; private set; }
    public bool ReadOnlyWindow { get; private set; }
    public bool EnableDualWrite { get; private set; }
    public bool EnableRollback { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ErrorSummary { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? StartedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? FinishedAt { get; private set; }

    public void MarkPrechecking(long userId, DateTimeOffset now)
    {
        Status = AppMigrationTaskStatuses.Prechecking;
        Phase = "Precheck";
        UpdatedBy = userId;
        UpdatedAt = now;
        ErrorSummary = null;
    }

    public void MarkReady(long userId, DateTimeOffset now)
    {
        Status = AppMigrationTaskStatuses.Ready;
        Phase = "Ready";
        UpdatedBy = userId;
        UpdatedAt = now;
        ErrorSummary = null;
    }

    public void MarkRunning(long userId, DateTimeOffset now, int totalItems)
    {
        Status = AppMigrationTaskStatuses.Running;
        Phase = "Executing";
        TotalItems = totalItems;
        CompletedItems = 0;
        FailedItems = 0;
        ProgressPercent = 0;
        StartedAt = now;
        UpdatedBy = userId;
        UpdatedAt = now;
        ErrorSummary = null;
    }

    public void MarkObjectProgress(string objectName, int batchNo, int completed, int failed, long userId, DateTimeOffset now)
    {
        CurrentObjectName = objectName;
        CurrentBatchNo = batchNo;
        CompletedItems = completed;
        FailedItems = failed;
        ProgressPercent = TotalItems <= 0
            ? 0
            : Math.Round((decimal)CompletedItems * 100m / TotalItems, 2, MidpointRounding.AwayFromZero);
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    public void MarkValidating(long userId, DateTimeOffset now)
    {
        Status = AppMigrationTaskStatuses.Validating;
        Phase = "Validating";
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    public void MarkCutoverReady(long userId, DateTimeOffset now)
    {
        Status = AppMigrationTaskStatuses.CutoverReady;
        Phase = "CutoverReady";
        ProgressPercent = 100;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    public void MarkCutoverCompleted(long userId, DateTimeOffset now, bool readOnlyWindow, bool enableDualWrite)
    {
        Status = AppMigrationTaskStatuses.CutoverCompleted;
        Phase = "CutoverCompleted";
        ReadOnlyWindow = readOnlyWindow;
        EnableDualWrite = enableDualWrite;
        FinishedAt = now;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    public void MarkFailed(long userId, DateTimeOffset now, string errorSummary)
    {
        Status = AppMigrationTaskStatuses.Failed;
        Phase = "Failed";
        ErrorSummary = errorSummary;
        FinishedAt = now;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    public void MarkRolledBack(long userId, DateTimeOffset now)
    {
        Status = AppMigrationTaskStatuses.RolledBack;
        Phase = "RolledBack";
        UpdatedBy = userId;
        UpdatedAt = now;
    }
}

public sealed class AppMigrationTaskItem : TenantEntity
{
    public AppMigrationTaskItem()
        : base(TenantId.Empty)
    {
        ObjectName = string.Empty;
        Status = AppMigrationTaskStatuses.Pending;
    }

    public AppMigrationTaskItem(
        TenantId tenantId,
        long taskId,
        string objectName,
        int batchNo,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TaskId = taskId;
        ObjectName = objectName;
        BatchNo = batchNo;
        Status = AppMigrationTaskStatuses.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long TaskId { get; private set; }
    public string ObjectName { get; private set; }
    public int BatchNo { get; private set; }
    public string Status { get; private set; }
    public int RowCount { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? Checksum { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void MarkSucceeded(int rowCount, string? checksum, DateTimeOffset now)
    {
        Status = "Succeeded";
        RowCount = rowCount;
        Checksum = checksum;
        ErrorMessage = null;
        UpdatedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        Status = "Failed";
        ErrorMessage = errorMessage;
        UpdatedAt = now;
    }
}

public sealed class AppMigrationProgressSnapshot : TenantEntity
{
    public AppMigrationProgressSnapshot()
        : base(TenantId.Empty)
    {
        Status = AppMigrationTaskStatuses.Pending;
        Phase = "Created";
    }

    public AppMigrationProgressSnapshot(
        TenantId tenantId,
        long taskId,
        string status,
        string phase,
        int totalItems,
        int completedItems,
        int failedItems,
        decimal progressPercent,
        string? currentObjectName,
        int? currentBatchNo,
        string? errorSummary,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TaskId = taskId;
        Status = status;
        Phase = phase;
        TotalItems = totalItems;
        CompletedItems = completedItems;
        FailedItems = failedItems;
        ProgressPercent = progressPercent;
        CurrentObjectName = currentObjectName;
        CurrentBatchNo = currentBatchNo;
        ErrorSummary = errorSummary;
        CreatedAt = now;
    }

    public long TaskId { get; private set; }
    public string Status { get; private set; }
    public string Phase { get; private set; }
    public int TotalItems { get; private set; }
    public int CompletedItems { get; private set; }
    public int FailedItems { get; private set; }
    public decimal ProgressPercent { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? CurrentObjectName { get; private set; }
    [SugarColumn(IsNullable = true)]
    public int? CurrentBatchNo { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ErrorSummary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class AppDataIntegrityReport : TenantEntity
{
    public AppDataIntegrityReport()
        : base(TenantId.Empty)
    {
        Status = "Pending";
    }

    public AppDataIntegrityReport(
        TenantId tenantId,
        long taskId,
        bool passed,
        int totalChecks,
        int passedChecks,
        int failedChecks,
        long checkedBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TaskId = taskId;
        Status = passed ? "Passed" : "Failed";
        TotalChecks = totalChecks;
        PassedChecks = passedChecks;
        FailedChecks = failedChecks;
        CheckedBy = checkedBy;
        CheckedAt = now;
        CreatedAt = now;
    }

    public long TaskId { get; private set; }
    public string Status { get; private set; }
    public int TotalChecks { get; private set; }
    public int PassedChecks { get; private set; }
    public int FailedChecks { get; private set; }
    public long CheckedBy { get; private set; }
    public DateTimeOffset CheckedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class AppIntegrityCheckItem : TenantEntity
{
    public AppIntegrityCheckItem()
        : base(TenantId.Empty)
    {
        CheckType = string.Empty;
        ObjectName = string.Empty;
        Status = "Pending";
    }

    public AppIntegrityCheckItem(
        TenantId tenantId,
        long reportId,
        string checkType,
        string objectName,
        bool passed,
        string? detailMessage,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ReportId = reportId;
        CheckType = checkType;
        ObjectName = objectName;
        Status = passed ? "Passed" : "Failed";
        DetailMessage = detailMessage;
        CreatedAt = now;
    }

    public long ReportId { get; private set; }
    public string CheckType { get; private set; }
    public string ObjectName { get; private set; }
    public string Status { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? DetailMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class AppDatabaseSchemaVersion : TenantEntity
{
    public AppDatabaseSchemaVersion()
        : base(TenantId.Empty)
    {
        Version = "0.0.0";
        Description = string.Empty;
    }

    public AppDatabaseSchemaVersion(
        TenantId tenantId,
        long appInstanceId,
        string version,
        string description,
        long appliedBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        Version = version;
        Description = description;
        AppliedBy = appliedBy;
        AppliedAt = now;
    }

    public long AppInstanceId { get; private set; }
    public string Version { get; private set; }
    public string Description { get; private set; }
    public long AppliedBy { get; private set; }
    public DateTimeOffset AppliedAt { get; private set; }
}

public sealed class AppDataRoutePolicy : TenantEntity
{
    public AppDataRoutePolicy()
        : base(TenantId.Empty)
    {
        Mode = "AppOnly";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public AppDataRoutePolicy(
        TenantId tenantId,
        long appInstanceId,
        string mode,
        bool readOnlyWindow,
        bool dualWriteEnabled,
        long updatedBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppInstanceId = appInstanceId;
        Mode = mode;
        ReadOnlyWindow = readOnlyWindow;
        DualWriteEnabled = dualWriteEnabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public long AppInstanceId { get; private set; }
    public string Mode { get; private set; }
    public bool ReadOnlyWindow { get; private set; }
    public bool DualWriteEnabled { get; private set; }
    public long UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void SetMode(string mode, bool readOnlyWindow, bool dualWriteEnabled, long updatedBy, DateTimeOffset now)
    {
        Mode = mode;
        ReadOnlyWindow = readOnlyWindow;
        DualWriteEnabled = dualWriteEnabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
