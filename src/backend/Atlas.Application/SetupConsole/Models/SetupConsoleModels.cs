namespace Atlas.Application.SetupConsole.Models;

// =============================================================================
// 总览 + 二次认证
// =============================================================================

public sealed record SetupConsoleOverviewDto(
    SystemSetupStateDto System,
    IReadOnlyList<WorkspaceSetupStateDto> Workspaces,
    DataMigrationJobDto? ActiveMigration,
    SetupConsoleCatalogSummaryDto CatalogSummary);

public sealed record SystemSetupStateDto(
    string State,
    string Version,
    DateTimeOffset LastUpdatedAt,
    string? FailureMessage,
    bool RecoveryKeyConfigured,
    IReadOnlyList<SetupStepRecordDto> Steps);

public sealed record SetupStepRecordDto(
    string Step,
    string State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int AttemptCount,
    string? ErrorMessage);

public sealed record WorkspaceSetupStateDto(
    string WorkspaceId,
    string WorkspaceName,
    string State,
    string SeedBundleVersion,
    DateTimeOffset LastUpdatedAt);

public sealed record SetupConsoleCatalogSummaryDto(
    int TotalEntities,
    int TotalCategories,
    IReadOnlyList<string> MissingCriticalTables,
    IReadOnlyList<SetupConsoleCatalogCategoryDto> Categories);

public sealed record SetupConsoleCatalogCategoryDto(
    string Category,
    string DisplayKey,
    int EntityCount,
    bool HasSeed);

public sealed record ConsoleAuthChallengeRequest(
    string? RecoveryKey,
    string? BootstrapAdminUsername,
    string? BootstrapAdminPassword);

public sealed record ConsoleAuthTokenDto(
    string ConsoleToken,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> Permissions);

// =============================================================================
// 系统级初始化（6 步）
// =============================================================================

public sealed record SetupStepResultDto(
    string Step,
    string State,
    string Message,
    string SystemState,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    IDictionary<string, object?>? Payload = null);

public sealed record SystemPrecheckRequest(
    string? ExpectedDbType,
    string? ExpectedConnectionString);

public sealed record SystemSchemaRequest(bool DryRun = false);

public sealed record SystemSeedRequest(
    string? BundleVersion,
    bool ForceReapply = false);

public sealed record SystemBootstrapUserRequest(
    string Username,
    string Password,
    string TenantId,
    bool IsPlatformAdmin,
    IReadOnlyList<string> OptionalRoleCodes,
    bool GenerateRecoveryKey);

public sealed record SystemBootstrapUserResponse(
    string Step,
    string State,
    string Message,
    string SystemState,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string? RecoveryKey,
    IDictionary<string, object?>? Payload = null);

public sealed record SystemDefaultWorkspaceRequest(
    string WorkspaceName,
    string OwnerUsername,
    bool ApplyDefaultPublishChannels,
    bool ApplyDefaultModelStub);

// =============================================================================
// 工作空间级初始化
// =============================================================================

public sealed record WorkspaceInitRequest(
    string WorkspaceName,
    string SeedBundleVersion,
    bool ApplyDefaultRoles,
    bool ApplyDefaultPublishChannels);

public sealed record WorkspaceSeedBundleRequest(
    string BundleVersion,
    bool ForceReapply = false);

// =============================================================================
// 数据迁移（M6 实施 OrmDataMigrationService 时填充）
// =============================================================================

public sealed record DbConnectionConfig(
    string DriverCode,
    string DbType,
    string Mode,
    string? ConnectionString,
    IDictionary<string, string>? VisualConfig,
    string? DisplayName = null,
    long? DataSourceId = null,
    long? AiDatabaseId = null);

public sealed record MigrationTestConnectionRequest(DbConnectionConfig Connection);

public sealed record MigrationTestConnectionResponse(
    bool Connected,
    string Message,
    string? DetectedDbType,
    int DetectedTableCount);

public sealed record DataMigrationModuleScopeDto(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string>? EntityNames);

public sealed record DataMigrationJobCreateRequest(
    DbConnectionConfig Source,
    DbConnectionConfig Target,
    string Mode,
    DataMigrationModuleScopeDto ModuleScope,
    bool AllowReExecute,
    IReadOnlyList<string>? SelectedEntities = null,
    IReadOnlyList<string>? SelectedTables = null,
    IReadOnlyList<string>? ExcludedEntities = null,
    IReadOnlyList<string>? ExcludedTables = null,
    int BatchSize = 10000,
    string WriteMode = "InsertOnly",
    bool CreateSchema = true,
    bool MigrateSystemTables = false,
    bool MigrateFiles = false,
    bool ValidateAfterCopy = false);

public sealed record DataMigrationJobDto(
    string Id,
    string State,
    string Mode,
    DbConnectionConfig Source,
    DbConnectionConfig Target,
    string SourceFingerprint,
    string TargetFingerprint,
    DataMigrationModuleScopeDto ModuleScope,
    int TotalEntities,
    int CompletedEntities,
    int FailedEntities,
    long TotalRows,
    long CopiedRows,
    decimal ProgressPercent,
    string? CurrentEntityName,
    string? CurrentTableName,
    int? CurrentBatchNo,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? ErrorSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DataMigrationPrecheckResultDto(
    DataMigrationJobDto Job,
    int TableCount,
    long TotalRows,
    int EstimatedBatches,
    IReadOnlyList<string> UnsupportedTables,
    IReadOnlyList<string> TargetNonEmptyTables,
    IReadOnlyList<string> MissingTargetTables,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<DataMigrationTableProgressDto> Tables);

public sealed record DataMigrationProgressDto(
    string JobId,
    string State,
    int TotalEntities,
    int CompletedEntities,
    int FailedEntities,
    long TotalRows,
    long CopiedRows,
    decimal ProgressPercent,
    string? CurrentEntityName,
    string? CurrentTableName,
    int? CurrentBatchNo,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long ElapsedSeconds,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DataMigrationTableProgressDto> Tables,
    IReadOnlyList<DataMigrationLogItemDto> RecentLogs,
    IReadOnlyList<DataMigrationBatchDto> RecentBatches);

public sealed record DataMigrationTableProgressDto(
    string EntityName,
    string TableName,
    string State,
    long SourceRows,
    long TargetRowsBefore,
    long TargetRowsAfter,
    long CopiedRows,
    long FailedRows,
    int BatchSize,
    int CurrentBatchNo,
    int TotalBatchCount,
    decimal ProgressPercent,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? ErrorMessage);

public sealed record DataMigrationBatchDto(
    int BatchNo,
    string EntityName,
    int RowsCopied,
    string State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string? Checksum);

public sealed record DataMigrationReportDto(
    string JobId,
    int TotalEntities,
    int PassedEntities,
    int FailedEntities,
    IReadOnlyList<DataMigrationRowDiffDto> RowDiff,
    IReadOnlyList<DataMigrationSamplingDiffDto> SamplingDiff,
    bool OverallPassed,
    DateTimeOffset GeneratedAt);

public sealed record DataMigrationRowDiffDto(
    string EntityName,
    string TableName,
    long SourceRowCount,
    long TargetRowCount,
    long Diff,
    string State = "passed",
    string? ErrorMessage = null);

public sealed record DataMigrationSamplingDiffDto(
    string EntityName,
    string TableName,
    int SampledRows,
    int Mismatched,
    IReadOnlyList<string> MismatchedExamples);

public sealed record DataMigrationLogItemDto(
    string Id,
    string JobId,
    string Level,
    string Module,
    string? EntityName,
    string Message,
    DateTimeOffset OccurredAt);

public sealed record DataMigrationLogPagedResponse(
    IReadOnlyList<DataMigrationLogItemDto> Items,
    int Total,
    int PageIndex,
    int PageSize);

public sealed record DataMigrationCutoverRequest(
    int KeepSourceReadonlyForDays = 7,
    bool ConfirmBackup = false,
    bool ConfirmRestartRequired = false);

public sealed record DataMigrationActionResultDto(
    bool Success,
    string JobId,
    string State,
    string? Message);
