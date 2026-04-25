using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiDatabaseListItem(
    long Id,
    string Name,
    string? Description,
    long? BotId,
    int RecordCount,
    int DraftRecordCount,
    int OnlineRecordCount,
    AiDatabaseQueryMode QueryMode,
    AiDatabaseChannelScope ChannelScope,
    AiDatabaseStorageMode StorageMode,
    string DriverCode,
    AiDatabaseProvisionState ProvisionState,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiDatabaseDetail(
    long Id,
    string Name,
    string? Description,
    long? BotId,
    string TableSchema,
    int RecordCount,
    int DraftRecordCount,
    int OnlineRecordCount,
    AiDatabaseQueryMode QueryMode,
    AiDatabaseChannelScope ChannelScope,
    AiDatabaseStorageMode StorageMode,
    string DriverCode,
    AiDatabaseProvisionState ProvisionState,
    string? ProvisionError,
    long? WorkspaceId,
    IReadOnlyList<AiDatabaseFieldItem> Fields,
    IReadOnlyList<AiDatabaseChannelConfigItem> ChannelConfigs,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiDatabaseCreateRequest(
    string Name,
    string? Description,
    long? BotId,
    string? TableSchema,
    long? WorkspaceId = null,
    IReadOnlyList<AiDatabaseFieldItem>? Fields = null,
    string DriverCode = "SQLite",
    AiDatabaseQueryMode QueryMode = AiDatabaseQueryMode.MultiUser,
    AiDatabaseChannelScope ChannelScope = AiDatabaseChannelScope.FullShared);

public sealed record AiDatabaseUpdateRequest(
    string Name,
    string? Description,
    long? BotId,
    string? TableSchema,
    long? WorkspaceId = null,
    IReadOnlyList<AiDatabaseFieldItem>? Fields = null,
    AiDatabaseQueryMode QueryMode = AiDatabaseQueryMode.MultiUser,
    AiDatabaseChannelScope ChannelScope = AiDatabaseChannelScope.FullShared);

public sealed record AiDatabaseFieldItem(
    long? Id,
    string Name,
    string? Description,
    string Type,
    bool Required,
    bool Indexed = false,
    bool IsSystemField = false,
    int SortOrder = 0);

public sealed record AiDatabaseChannelConfigItem(
    string ChannelKey,
    string DisplayName,
    bool AllowDraft,
    bool AllowOnline,
    string? PublishChannelType = null,
    string? CredentialKind = null,
    int SortOrder = 0);

public sealed record AiDatabaseRecordListItem(
    long Id,
    long DatabaseId,
    string DataJson,
    AiDatabaseRecordEnvironment Environment,
    long? OwnerUserId,
    long? CreatorUserId,
    string? ChannelId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiDatabaseRecordCreateRequest(
    string DataJson,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft);

public sealed record AiDatabaseRecordUpdateRequest(
    string DataJson,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft);

/// <summary>D5：批量记录新增请求。Rows 每项是单条记录的 DataJson。</summary>
public sealed record AiDatabaseRecordBulkCreateRequest(
    IReadOnlyList<string> Rows,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft);

/// <summary>D5：批量同步插入结果（每条记录的成功 / 失败 / id 明细）。</summary>
public sealed record AiDatabaseRecordBulkCreateResult(
    int Total,
    int Succeeded,
    int Failed,
    IReadOnlyList<AiDatabaseRecordBulkRowResult> Rows);

public sealed record AiDatabaseRecordBulkRowResult(
    int Index,
    bool Success,
    string? Id,
    string? ErrorMessage);

/// <summary>D5：异步批量任务提交结果。</summary>
public sealed record AiDatabaseBulkJobAccepted(long TaskId, int RowCount);

public sealed record AiDatabaseSchemaValidateRequest(string TableSchema);

public sealed record AiDatabaseSchemaValidateResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record AiDatabaseImportRequest(
    long FileId,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft);

public sealed record AiDatabaseModeUpdateRequest(
    AiDatabaseQueryMode QueryMode,
    AiDatabaseChannelScope ChannelScope);

public sealed record AiDatabaseChannelConfigsUpdateRequest(
    IReadOnlyList<AiDatabaseChannelConfigItem> Items);

public sealed record AiDatabaseImportProgress(
    long TaskId,
    long DatabaseId,
    AiDatabaseImportStatus Status,
    int TotalRows,
    int SucceededRows,
    int FailedRows,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    AiDatabaseImportSource Source = AiDatabaseImportSource.File,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft);

public sealed record AiDatabaseTemplate(
    string FileName,
    string ContentType,
    byte[] Content);
