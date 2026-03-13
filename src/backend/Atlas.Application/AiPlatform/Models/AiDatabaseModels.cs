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
    string TableSchema);

public sealed record AiDatabaseUpdateRequest(
    string Name,
    string? Description,
    long? BotId,
    string TableSchema);

public sealed record AiDatabaseRecordListItem(
    long Id,
    long DatabaseId,
    string DataJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiDatabaseRecordCreateRequest(string DataJson);

public sealed record AiDatabaseRecordUpdateRequest(string DataJson);

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
    DateTime? UpdatedAt);

public sealed record AiDatabaseTemplate(
    string FileName,
    string ContentType,
    byte[] Content);
