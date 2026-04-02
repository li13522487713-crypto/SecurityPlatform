using Atlas.Core.Expressions;

namespace Atlas.Application.LogicFlow.Expressions.Models;

public sealed record FunctionDefinitionCreateRequest(
    string Name,
    string? DisplayName,
    string? Description,
    FunctionCategory Category,
    string ParametersJson,
    ExprType ReturnType,
    string? BodyExpression,
    int SortOrder = 0);

public sealed record FunctionDefinitionUpdateRequest(
    long Id,
    string Name,
    string? DisplayName,
    string? Description,
    FunctionCategory Category,
    string ParametersJson,
    ExprType ReturnType,
    string? BodyExpression,
    bool IsEnabled,
    int SortOrder = 0);

public sealed record FunctionDefinitionResponse(
    long Id,
    string Name,
    string? DisplayName,
    string? Description,
    FunctionCategory Category,
    string ParametersJson,
    ExprType ReturnType,
    string? BodyExpression,
    bool IsBuiltin,
    bool IsEnabled,
    int SortOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

public sealed record FunctionDefinitionListItem(
    long Id,
    string Name,
    string? DisplayName,
    FunctionCategory Category,
    ExprType ReturnType,
    bool IsBuiltin,
    bool IsEnabled,
    int SortOrder);
