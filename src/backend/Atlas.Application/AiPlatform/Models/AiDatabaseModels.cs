using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiDatabaseListItem(
    long Id,
    string Name,
    string? Description,
    long? BotId,
    int RecordCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiDatabaseDetail(
    long Id,
    string Name,
    string? Description,
    long? BotId,
    string TableSchema,
    int RecordCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiDatabaseCreateRequest(
    string Name,
    string? Description,
    long? BotId,
    string TableSchema,
    long? WorkspaceId = null);

public sealed record AiDatabaseUpdateRequest(
    string Name,
    string? Description,
    long? BotId,
    string TableSchema,
    long? WorkspaceId = null);

public sealed record AiDatabaseRecordListItem(
    long Id,
    long DatabaseId,
    string DataJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiDatabaseRecordCreateRequest(string DataJson);

public sealed record AiDatabaseRecordUpdateRequest(string DataJson);

/// <summary>D5：批量记录新增请求。Rows 每项是单条记录的 DataJson。</summary>
public sealed record AiDatabaseRecordBulkCreateRequest(IReadOnlyList<string> Rows);

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

public sealed record AiDatabaseImportRequest(long FileId);

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
    AiDatabaseImportSource Source = AiDatabaseImportSource.File);

public sealed record AiDatabaseTemplate(
    string FileName,
    string ContentType,
    byte[] Content);
