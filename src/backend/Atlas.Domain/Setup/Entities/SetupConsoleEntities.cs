using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Setup.Entities;

/// <summary>
/// 系统初始化与迁移控制台的 8 张元数据表。
/// 全部归属 <c>Atlas.Domain.Setup.Entities</c>，由 <c>AtlasOrmSchemaCatalog</c> 统一建表。
/// </summary>
public static class SystemSetupStates
{
    public const string NotStarted = "not_started";
    public const string PrecheckPassed = "precheck_passed";
    public const string SchemaInitializing = "schema_initializing";
    public const string SchemaInitialized = "schema_initialized";
    public const string SeedInitializing = "seed_initializing";
    public const string SeedInitialized = "seed_initialized";
    public const string MigrationPending = "migration_pending";
    public const string MigrationRunning = "migration_running";
    public const string MigrationPartiallyCompleted = "migration_partially_completed";
    public const string MigrationCompleted = "migration_completed";
    public const string ValidationRunning = "validation_running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Dismissed = "dismissed";
}

public static class WorkspaceSetupStates
{
    public const string Pending = "workspace_init_pending";
    public const string Running = "workspace_init_running";
    public const string Completed = "workspace_init_completed";
    public const string Failed = "workspace_init_failed";
}

public static class SetupStepStates
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

public static class SetupConsoleSteps
{
    public const string Precheck = "precheck";
    public const string Schema = "schema";
    public const string Seed = "seed";
    public const string BootstrapUser = "bootstrap-user";
    public const string DefaultWorkspace = "default-workspace";
    public const string Complete = "complete";
}

public static class DataMigrationModes
{
    public const string StructureOnly = "structure-only";
    public const string StructurePlusData = "structure-plus-data";
    public const string ValidateOnly = "validate-only";
    public const string IncrementalDelta = "incremental-delta";
    public const string ReExecute = "re-execute";
}

public static class DataMigrationConnectionModes
{
    public const string CurrentSystem = "CurrentSystem";
    public const string CurrentSystemAiDatabase = "CurrentSystemAiDatabase";
    public const string SavedDataSource = "SavedDataSource";
    public const string ConnectionString = "ConnectionString";
    public const string VisualConfig = "VisualConfig";
    public const string Raw = "raw";
    public const string Visual = "visual";
}

public static class DataMigrationWriteModes
{
    public const string InsertOnly = "InsertOnly";
    public const string TruncateThenInsert = "TruncateThenInsert";
    public const string Upsert = "Upsert";
}

public static class DataMigrationStates
{
    public const string Created = "created";
    public const string Pending = "pending";
    public const string Prechecking = "prechecking";
    public const string Ready = "ready";
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Cancelling = "cancelling";
    public const string Cancelled = "cancelled";
    public const string Succeeded = "succeeded";
    public const string Validating = "validating";
    public const string ValidationFailed = "validation_failed";
    public const string Validated = "validated";
    public const string CutoverReady = "cutover-ready";
    public const string CutoverCompleted = "cutover-completed";
    public const string CutoverFailed = "cutover_failed";
    public const string Failed = "failed";
    public const string RolledBack = "rolled-back";
}

public static class DataMigrationTableStates
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
    public const string Cancelled = "cancelled";
}

[SugarTable("setup_system_state")]
public sealed class SystemSetupState : TenantEntity
{
    public SystemSetupState() : base(TenantId.Empty)
    {
        State = SystemSetupStates.NotStarted;
        Version = "v1";
    }

    public SystemSetupState(TenantId tenantId, long id, string version, DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        State = SystemSetupStates.NotStarted;
        Version = version;
        LastUpdatedAt = now;
    }

    public string State { get; private set; }
    public string Version { get; private set; }
    public DateTimeOffset LastUpdatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? FailureMessage { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? RecoveryKeyHash { get; private set; }

    /// <summary>
    /// BootstrapAdmin 密码的 PBKDF2 哈希（M8/A3）。
    /// PlatformHost 启动时把 appsettings 中明文密码哈希后写入；登录时不再明文比对。
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public string? BootstrapPasswordHash { get; private set; }

    /// <summary>仅内存计算（不走 SqlSugar 列扫描）：判断恢复密钥是否已配置。</summary>
    public bool IsRecoveryKeyConfigured() => !string.IsNullOrEmpty(RecoveryKeyHash);

    public void TransitionTo(string nextState, DateTimeOffset now, string? failureMessage = null)
    {
        State = nextState;
        FailureMessage = failureMessage;
        LastUpdatedAt = now;
    }

    public void SetRecoveryKeyHash(string hash, DateTimeOffset now)
    {
        RecoveryKeyHash = hash;
        LastUpdatedAt = now;
    }

    public void SetBootstrapPasswordHash(string hash, DateTimeOffset now)
    {
        BootstrapPasswordHash = hash;
        LastUpdatedAt = now;
    }

    public void SetVersion(string version, DateTimeOffset now)
    {
        Version = version;
        LastUpdatedAt = now;
    }
}

/// <summary>
/// 种子 bundle 应用日志（M8/B1）。
///
/// 每个种子模块（roles / menus / dictionaries / model-configs ...）在每个 version
/// 下只记录一条 succeeded；防重复触发 + 升级 v1->v2 增量补种依赖此表。
/// </summary>
[SugarTable("setup_seed_bundle_log")]
public sealed class SetupSeedBundleLog : TenantEntity
{
    public SetupSeedBundleLog() : base(TenantId.Empty)
    {
        Bundle = string.Empty;
        Version = string.Empty;
    }

    public SetupSeedBundleLog(
        TenantId tenantId,
        long id,
        string bundle,
        string version,
        DateTimeOffset appliedAt) : base(tenantId)
    {
        Id = id;
        Bundle = bundle;
        Version = version;
        AppliedAt = appliedAt;
    }

    public string Bundle { get; private set; }

    public string Version { get; private set; }

    public DateTimeOffset AppliedAt { get; private set; }
}

[SugarTable("setup_workspace_state")]
public sealed class WorkspaceSetupState : TenantEntity
{
    public WorkspaceSetupState() : base(TenantId.Empty)
    {
        State = WorkspaceSetupStates.Pending;
        WorkspaceId = string.Empty;
        WorkspaceName = string.Empty;
        SeedBundleVersion = "v0";
    }

    public WorkspaceSetupState(
        TenantId tenantId,
        long id,
        string workspaceId,
        string workspaceName,
        string seedBundleVersion,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        WorkspaceName = workspaceName;
        State = WorkspaceSetupStates.Pending;
        SeedBundleVersion = seedBundleVersion;
        LastUpdatedAt = now;
    }

    public string WorkspaceId { get; private set; }
    public string WorkspaceName { get; private set; }
    public string State { get; private set; }
    public string SeedBundleVersion { get; private set; }
    public DateTimeOffset LastUpdatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? FailureMessage { get; private set; }

    public void TransitionTo(string nextState, DateTimeOffset now, string? failureMessage = null)
    {
        State = nextState;
        FailureMessage = failureMessage;
        LastUpdatedAt = now;
    }

    public void SetSeedBundleVersion(string version, DateTimeOffset now)
    {
        SeedBundleVersion = version;
        LastUpdatedAt = now;
    }

    public void SetWorkspaceName(string name, DateTimeOffset now)
    {
        WorkspaceName = name;
        LastUpdatedAt = now;
    }
}

[SugarTable("setup_step_record")]
public sealed class SetupStepRecord : TenantEntity
{
    public SetupStepRecord() : base(TenantId.Empty)
    {
        Step = string.Empty;
        State = SetupStepStates.Running;
        Scope = "system";
    }

    public SetupStepRecord(
        TenantId tenantId,
        long id,
        string scope,
        string step,
        string state,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        Scope = scope;
        Step = step;
        State = state;
        AttemptCount = 1;
        StartedAt = now;
        EndedAt = state == SetupStepStates.Running ? null : now;
    }

    /// <summary>system | workspace</summary>
    public string Scope { get; private set; }

    public string Step { get; private set; }
    public string State { get; private set; }
    public int AttemptCount { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? StartedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? EndedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? PayloadJson { get; private set; }

    public void Restart(DateTimeOffset now)
    {
        State = SetupStepStates.Running;
        AttemptCount += 1;
        StartedAt = now;
        EndedAt = null;
        ErrorMessage = null;
    }

    public void MarkSucceeded(DateTimeOffset now, string? payloadJson = null)
    {
        State = SetupStepStates.Succeeded;
        EndedAt = now;
        ErrorMessage = null;
        if (payloadJson is not null)
        {
            PayloadJson = payloadJson;
        }
    }

    public void MarkFailed(DateTimeOffset now, string errorMessage)
    {
        State = SetupStepStates.Failed;
        EndedAt = now;
        ErrorMessage = errorMessage;
    }

    public void MarkSkipped(DateTimeOffset now)
    {
        State = SetupStepStates.Skipped;
        EndedAt = now;
    }
}

[SugarTable("setup_data_migration_job")]
public sealed class DataMigrationJob : TenantEntity
{
    public DataMigrationJob() : base(TenantId.Empty)
    {
        State = DataMigrationStates.Pending;
        Mode = DataMigrationModes.StructurePlusData;
        SourceConnectionString = string.Empty;
        SourceDbType = string.Empty;
        TargetConnectionString = string.Empty;
        TargetDbType = string.Empty;
        SourceFingerprint = string.Empty;
        TargetFingerprint = string.Empty;
        ModuleScopeJson = "{}";
        SourceConfigJson = "{}";
        TargetConfigJson = "{}";
        SelectedEntitiesJson = "[]";
        SelectedTablesJson = "[]";
        ExcludedEntitiesJson = "[]";
        ExcludedTablesJson = "[]";
        WriteMode = DataMigrationWriteModes.InsertOnly;
    }

    public DataMigrationJob(
        TenantId tenantId,
        long id,
        string mode,
        string sourceConnectionString,
        string sourceDbType,
        string targetConnectionString,
        string targetDbType,
        string sourceFingerprint,
        string targetFingerprint,
        string moduleScopeJson,
        string sourceConfigJson,
        string targetConfigJson,
        string selectedEntitiesJson,
        string selectedTablesJson,
        string excludedEntitiesJson,
        string excludedTablesJson,
        int batchSize,
        string writeMode,
        bool createSchema,
        bool migrateSystemTables,
        bool migrateFiles,
        bool validateAfterCopy,
        long createdBy,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        State = DataMigrationStates.Pending;
        Mode = mode;
        SourceConnectionString = sourceConnectionString;
        SourceDbType = sourceDbType;
        TargetConnectionString = targetConnectionString;
        TargetDbType = targetDbType;
        SourceFingerprint = sourceFingerprint;
        TargetFingerprint = targetFingerprint;
        ModuleScopeJson = moduleScopeJson;
        SourceConfigJson = sourceConfigJson;
        TargetConfigJson = targetConfigJson;
        SelectedEntitiesJson = selectedEntitiesJson;
        SelectedTablesJson = selectedTablesJson;
        ExcludedEntitiesJson = excludedEntitiesJson;
        ExcludedTablesJson = excludedTablesJson;
        BatchSize = batchSize;
        WriteMode = writeMode;
        CreateSchema = createSchema;
        MigrateSystemTables = migrateSystemTables;
        MigrateFiles = migrateFiles;
        ValidateAfterCopy = validateAfterCopy;
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public DataMigrationJob(
        TenantId tenantId,
        long id,
        string mode,
        string sourceConnectionString,
        string sourceDbType,
        string targetConnectionString,
        string targetDbType,
        string sourceFingerprint,
        string targetFingerprint,
        string moduleScopeJson,
        long createdBy,
        DateTimeOffset now) : this(
        tenantId,
        id,
        mode,
        sourceConnectionString,
        sourceDbType,
        targetConnectionString,
        targetDbType,
        sourceFingerprint,
        targetFingerprint,
        moduleScopeJson,
        sourceConfigJson: "{}",
        targetConfigJson: "{}",
        selectedEntitiesJson: "[]",
        selectedTablesJson: "[]",
        excludedEntitiesJson: "[]",
        excludedTablesJson: "[]",
        batchSize: 10000,
        writeMode: DataMigrationWriteModes.InsertOnly,
        createSchema: true,
        migrateSystemTables: false,
        migrateFiles: false,
        validateAfterCopy: false,
        createdBy: createdBy,
        now: now)
    {
    }

    public string State { get; private set; }
    public string Mode { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SourceConnectionString { get; private set; }

    public string SourceDbType { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string TargetConnectionString { get; private set; }

    public string TargetDbType { get; private set; }

    public string SourceFingerprint { get; private set; }
    public string TargetFingerprint { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ModuleScopeJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SourceConfigJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string TargetConfigJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SelectedEntitiesJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SelectedTablesJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ExcludedEntitiesJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ExcludedTablesJson { get; private set; }

    public int BatchSize { get; private set; }
    public string WriteMode { get; private set; }
    public bool CreateSchema { get; private set; }
    public bool MigrateSystemTables { get; private set; }
    public bool MigrateFiles { get; private set; }
    public bool ValidateAfterCopy { get; private set; }

    public int TotalEntities { get; private set; }
    public int CompletedEntities { get; private set; }
    public int FailedEntities { get; private set; }
    public long TotalRows { get; private set; }
    public long CopiedRows { get; private set; }
    public decimal ProgressPercent { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? CurrentEntityName { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? CurrentTableName { get; private set; }

    [SugarColumn(IsNullable = true)]
    public int? CurrentBatchNo { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorSummary { get; private set; }

    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? StartedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? FinishedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? QueuedAt { get; private set; }

    public void TransitionTo(string nextState, DateTimeOffset now, string? errorSummary = null)
    {
        State = nextState;
        ErrorSummary = errorSummary;
        UpdatedAt = now;
    }

    public void MarkQueued(DateTimeOffset now)
    {
        State = DataMigrationStates.Queued;
        QueuedAt = now;
        UpdatedAt = now;
        ErrorSummary = null;
    }

    public void MarkRunning(int totalEntities, long totalRows, DateTimeOffset now)
    {
        State = DataMigrationStates.Running;
        TotalEntities = totalEntities;
        TotalRows = totalRows;
        CompletedEntities = 0;
        FailedEntities = 0;
        CopiedRows = 0;
        ProgressPercent = 0;
        StartedAt = now;
        UpdatedAt = now;
        ErrorSummary = null;
    }

    public void RecordProgress(
        string currentEntity,
        string? currentTable,
        int currentBatch,
        int completedEntities,
        int failedEntities,
        long copiedRows,
        decimal progressPercent,
        DateTimeOffset now)
    {
        CurrentEntityName = currentEntity;
        CurrentTableName = currentTable;
        CurrentBatchNo = currentBatch;
        CompletedEntities = completedEntities;
        FailedEntities = failedEntities;
        CopiedRows = copiedRows;
        ProgressPercent = progressPercent;
        UpdatedAt = now;
    }

    public void RecordProgress(
        string currentEntity,
        int currentBatch,
        int completedEntities,
        int failedEntities,
        long copiedRows,
        DateTimeOffset now)
    {
        var percent = TotalEntities <= 0
            ? 0m
            : Math.Round((decimal)completedEntities * 100m / TotalEntities, 2, MidpointRounding.AwayFromZero);
        RecordProgress(currentEntity, null, currentBatch, completedEntities, failedEntities, copiedRows, percent, now);
    }

    public void RecordProgress(
        string currentEntity,
        int currentBatch,
        int completedEntities,
        int failedEntities,
        long copiedRows,
        decimal progressPercent,
        DateTimeOffset now)
    {
        RecordProgress(currentEntity, null, currentBatch, completedEntities, failedEntities, copiedRows, progressPercent, now);
    }

    public void MarkFinished(string finalState, DateTimeOffset now)
    {
        State = finalState;
        FinishedAt = now;
        ProgressPercent = finalState is DataMigrationStates.Succeeded or DataMigrationStates.Validated or DataMigrationStates.CutoverCompleted
            ? 100m
            : ProgressPercent;
        UpdatedAt = now;
    }
}

[SugarTable("setup_data_migration_batch")]
public sealed class DataMigrationBatch : TenantEntity
{
    public DataMigrationBatch() : base(TenantId.Empty)
    {
        EntityName = string.Empty;
        TableName = string.Empty;
        State = DataMigrationTableStates.Running;
    }

    public DataMigrationBatch(
        TenantId tenantId,
        long id,
        long jobId,
        string entityName,
        string tableName,
        int batchNo,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        EntityName = entityName;
        TableName = tableName;
        BatchNo = batchNo;
        State = DataMigrationTableStates.Running;
        StartedAt = now;
    }

    public DataMigrationBatch(
        TenantId tenantId,
        long id,
        long jobId,
        string entityName,
        int batchNo,
        DateTimeOffset now) : this(
        tenantId,
        id,
        jobId,
        entityName,
        entityName,
        batchNo,
        now)
    {
    }

    public long JobId { get; private set; }
    public string EntityName { get; private set; }
    public string TableName { get; private set; }
    public int BatchNo { get; private set; }
    public string State { get; private set; }
    public int RowsCopied { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? Checksum { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? EndedAt { get; private set; }

    public void MarkSucceeded(int rowsCopied, string? checksum, DateTimeOffset now)
    {
        State = DataMigrationTableStates.Succeeded;
        RowsCopied = rowsCopied;
        Checksum = checksum;
        EndedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        State = DataMigrationTableStates.Failed;
        ErrorMessage = errorMessage;
        EndedAt = now;
    }
}

[SugarTable("setup_data_migration_checkpoint")]
public sealed class DataMigrationCheckpoint : TenantEntity
{
    public DataMigrationCheckpoint() : base(TenantId.Empty)
    {
        EntityName = string.Empty;
    }

    public DataMigrationCheckpoint(
        TenantId tenantId,
        long id,
        long jobId,
        string entityName,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        EntityName = entityName;
        UpdatedAt = now;
    }

    public long JobId { get; private set; }
    public string EntityName { get; private set; }
    public int LastBatchNo { get; private set; }
    public long LastMaxId { get; private set; }
    public long RowsCopied { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Advance(int lastBatchNo, long lastMaxId, long rowsCopied, DateTimeOffset now)
    {
        LastBatchNo = lastBatchNo;
        LastMaxId = lastMaxId;
        RowsCopied = rowsCopied;
        UpdatedAt = now;
    }
}

[SugarTable("setup_data_migration_table_progress")]
public sealed class DataMigrationTableProgress : TenantEntity
{
    public DataMigrationTableProgress() : base(TenantId.Empty)
    {
        EntityName = string.Empty;
        TableName = string.Empty;
        State = DataMigrationTableStates.Pending;
    }

    public DataMigrationTableProgress(
        TenantId tenantId,
        long id,
        long jobId,
        string entityName,
        string tableName,
        int batchSize,
        int totalBatchCount,
        long sourceRows,
        long targetRowsBefore,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        EntityName = entityName;
        TableName = tableName;
        BatchSize = batchSize;
        TotalBatchCount = totalBatchCount;
        SourceRows = sourceRows;
        TargetRowsBefore = targetRowsBefore;
        State = DataMigrationTableStates.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long JobId { get; private set; }
    public string EntityName { get; private set; }
    public string TableName { get; private set; }
    public string State { get; private set; }
    public long SourceRows { get; private set; }
    public long TargetRowsBefore { get; private set; }
    public long TargetRowsAfter { get; private set; }
    public long CopiedRows { get; private set; }
    public long FailedRows { get; private set; }
    public int BatchSize { get; private set; }
    public int CurrentBatchNo { get; private set; }
    public int TotalBatchCount { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? LastMaxId { get; private set; }
    public decimal ProgressPercent { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? StartedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? FinishedAt { get; private set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? ErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void ResetForRetry(long sourceRows, long targetRowsBefore, int batchSize, int totalBatchCount, DateTimeOffset now)
    {
        SourceRows = sourceRows;
        TargetRowsBefore = targetRowsBefore;
        TargetRowsAfter = 0;
        CopiedRows = 0;
        FailedRows = 0;
        BatchSize = batchSize;
        CurrentBatchNo = 0;
        TotalBatchCount = totalBatchCount;
        LastMaxId = null;
        ProgressPercent = 0;
        StartedAt = null;
        FinishedAt = null;
        ErrorMessage = null;
        State = DataMigrationTableStates.Pending;
        UpdatedAt = now;
    }

    public void MarkRunning(DateTimeOffset now)
    {
        State = DataMigrationTableStates.Running;
        StartedAt ??= now;
        UpdatedAt = now;
        ErrorMessage = null;
    }

    public void RecordBatch(int currentBatchNo, long copiedRows, string? lastMaxId, DateTimeOffset now)
    {
        CurrentBatchNo = currentBatchNo;
        CopiedRows = copiedRows;
        LastMaxId = lastMaxId;
        ProgressPercent = SourceRows <= 0
            ? 100m
            : Math.Round(copiedRows * 100m / SourceRows, 2, MidpointRounding.AwayFromZero);
        UpdatedAt = now;
    }

    public void MarkSucceeded(long targetRowsAfter, DateTimeOffset now)
    {
        State = DataMigrationTableStates.Succeeded;
        TargetRowsAfter = targetRowsAfter;
        FinishedAt = now;
        ProgressPercent = 100m;
        UpdatedAt = now;
    }

    public void MarkFailed(string errorMessage, long failedRows, DateTimeOffset now)
    {
        State = DataMigrationTableStates.Failed;
        FailedRows = failedRows;
        ErrorMessage = errorMessage;
        FinishedAt = now;
        UpdatedAt = now;
    }

    public void MarkCancelled(long targetRowsAfter, DateTimeOffset now)
    {
        State = DataMigrationTableStates.Cancelled;
        TargetRowsAfter = targetRowsAfter;
        FinishedAt = now;
        UpdatedAt = now;
    }

    public void UpdateTargetRowsAfter(long targetRowsAfter, DateTimeOffset now)
    {
        TargetRowsAfter = targetRowsAfter;
        UpdatedAt = now;
    }
}

[SugarTable("setup_data_migration_log")]
public sealed class DataMigrationLog : TenantEntity
{
    public DataMigrationLog() : base(TenantId.Empty)
    {
        Level = "info";
        Module = string.Empty;
        Message = string.Empty;
    }

    public DataMigrationLog(
        TenantId tenantId,
        long id,
        long jobId,
        string level,
        string module,
        string message,
        string? entityName,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        Level = level;
        Module = module;
        Message = message;
        EntityName = entityName;
        OccurredAt = now;
    }

    public long JobId { get; private set; }
    public string Level { get; private set; }
    public string Module { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string Message { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? EntityName { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}

/// <summary>
/// 控制台二次认证 token 持久化（M8/A3）。
///
/// 替代 M5 的内存 ConcurrentDictionary，让多实例部署 / 进程重启都能共享 token 生命周期。
/// 仅存哈希（PBKDF2），明文 token 仅在颁发时返回给客户端。
/// </summary>
[SugarTable("setup_console_token")]
public sealed class SetupConsoleToken : TenantEntity
{
    public SetupConsoleToken() : base(TenantId.Empty)
    {
        TokenHash = string.Empty;
        Permissions = string.Empty;
    }

    public SetupConsoleToken(
        TenantId tenantId,
        long id,
        string tokenHash,
        string permissions,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt) : base(tenantId)
    {
        Id = id;
        TokenHash = tokenHash;
        Permissions = permissions;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    [SugarColumn(IndexGroupNameList = new[] { "ix_setup_console_token_hash" })]
    public string TokenHash { get; private set; }

    /// <summary>逗号分隔的权限范围（system,workspace,migration）。</summary>
    public string Permissions { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Renew(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt = now;
    }
}

[SugarTable("setup_data_migration_report")]
public sealed class DataMigrationReport : TenantEntity
{
    public DataMigrationReport() : base(TenantId.Empty)
    {
        RowDiffJson = "[]";
        SamplingDiffJson = "[]";
    }

    public DataMigrationReport(
        TenantId tenantId,
        long id,
        long jobId,
        int totalEntities,
        int passedEntities,
        int failedEntities,
        bool overallPassed,
        string rowDiffJson,
        string samplingDiffJson,
        DateTimeOffset now) : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        TotalEntities = totalEntities;
        PassedEntities = passedEntities;
        FailedEntities = failedEntities;
        OverallPassed = overallPassed;
        RowDiffJson = rowDiffJson;
        SamplingDiffJson = samplingDiffJson;
        GeneratedAt = now;
    }

    public long JobId { get; private set; }
    public int TotalEntities { get; private set; }
    public int PassedEntities { get; private set; }
    public int FailedEntities { get; private set; }
    public bool OverallPassed { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string RowDiffJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SamplingDiffJson { get; private set; }

    public DateTimeOffset GeneratedAt { get; private set; }
}
