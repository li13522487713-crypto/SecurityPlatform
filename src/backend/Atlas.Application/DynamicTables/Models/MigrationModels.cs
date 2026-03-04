namespace Atlas.Application.DynamicTables.Models;

public sealed record MigrationRecordListItem(
    string Id,
    string TableKey,
    int Version,
    string Status,
    bool IsDestructive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExecutedAt,
    long CreatedBy,
    string? ErrorMessage);

public sealed record MigrationRecordDetail(
    string Id,
    string TableKey,
    int Version,
    string Status,
    string UpScript,
    string? DownScript,
    bool IsDestructive,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExecutedAt,
    long CreatedBy,
    long UpdatedBy);

public sealed record MigrationRecordCreateRequest(
    string TableKey,
    int Version,
    string UpScript,
    string? DownScript,
    bool IsDestructive);

public sealed record MigrationScriptPreview(
    string TableKey,
    string UpScript,
    string? DownScript,
    bool IsDestructive,
    IReadOnlyList<string> Warnings);

public sealed record MigrationExecutionResult(
    string Id,
    string TableKey,
    int Version,
    string Status,
    DateTimeOffset? ExecutedAt,
    string? ErrorMessage);

public sealed record MigrationPrecheckResult(
    string Id,
    string TableKey,
    int Version,
    bool RequiresConfirmation,
    bool CanExecute,
    IReadOnlyList<string> Checks);

public sealed record MigrationExecuteRequest(bool ConfirmDestructive);
