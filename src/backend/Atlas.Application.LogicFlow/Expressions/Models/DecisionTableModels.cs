using Atlas.Core.Expressions;

namespace Atlas.Application.LogicFlow.Expressions.Models;

public sealed record DecisionTableCreateRequest(
    string Name,
    string? DisplayName,
    string? Description,
    DecisionHitPolicy HitPolicy,
    string InputColumnsJson,
    string OutputColumnsJson,
    string RowsJson,
    int SortOrder = 0);

public sealed record DecisionTableUpdateRequest(
    long Id,
    string Name,
    string? DisplayName,
    string? Description,
    DecisionHitPolicy HitPolicy,
    string InputColumnsJson,
    string OutputColumnsJson,
    string RowsJson,
    bool IsEnabled,
    int SortOrder = 0);

public sealed record DecisionTableResponse(
    long Id,
    string Name,
    string? DisplayName,
    string? Description,
    DecisionHitPolicy HitPolicy,
    string InputColumnsJson,
    string OutputColumnsJson,
    string RowsJson,
    bool IsEnabled,
    int SortOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record DecisionTableListItem(
    long Id,
    string Name,
    string? DisplayName,
    DecisionHitPolicy HitPolicy,
    bool IsEnabled,
    int SortOrder);

public sealed record DecisionTableExecuteRequest(
    long TableId,
    IReadOnlyDictionary<string, object?> Input);

public sealed record DecisionTableExecuteResponse(
    bool IsMatched,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> MatchedOutputs);
