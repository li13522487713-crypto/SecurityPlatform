namespace Atlas.Application.DynamicTables.Models;

public sealed record SchemaDraftListItem(
    long Id,
    string ObjectType,
    string ObjectKey,
    string ChangeType,
    string RiskLevel,
    string Status,
    string? ValidationMessage,
    string? AfterSnapshot,
    string? BeforeSnapshot,
    DateTimeOffset CreatedAt,
    long CreatedBy);

public sealed record DynamicSchemaDraftCreateRequest(
    long AppInstanceId,
    string ObjectType,
    string ObjectKey,
    string ObjectId,
    string ChangeType,
    string? BeforeSnapshot,
    string? AfterSnapshot,
    string RiskLevel);

public sealed record SchemaDraftPublishResult(
    bool Success,
    int PublishedCount,
    IReadOnlyList<string> Errors);
