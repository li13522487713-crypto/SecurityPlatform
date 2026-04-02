namespace Atlas.Application.LogicFlow.Expressions.Models;

public sealed record RuleChainCreateRequest(
    string Name,
    string? DisplayName,
    string? Description,
    string StepsJson,
    string? DefaultOutputExpression,
    int SortOrder = 0);

public sealed record RuleChainUpdateRequest(
    long Id,
    string Name,
    string? DisplayName,
    string? Description,
    string StepsJson,
    string? DefaultOutputExpression,
    bool IsEnabled,
    int SortOrder = 0);

public sealed record RuleChainResponse(
    long Id,
    string Name,
    string? DisplayName,
    string? Description,
    string StepsJson,
    string? DefaultOutputExpression,
    bool IsEnabled,
    int SortOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record RuleChainListItem(
    long Id,
    string Name,
    string? DisplayName,
    bool IsEnabled,
    int SortOrder);

public sealed record RuleChainExecuteRequest(
    long ChainId,
    IReadOnlyDictionary<string, object?> Input);

public sealed record RuleChainExecuteResponse(
    bool IsMatched,
    object? Output,
    int? MatchedStepIndex);
