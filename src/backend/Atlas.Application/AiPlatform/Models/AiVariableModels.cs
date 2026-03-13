using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiVariableListItem(
    long Id,
    string Key,
    string? Value,
    AiVariableScope Scope,
    long? ScopeId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiVariableDetail(
    long Id,
    string Key,
    string? Value,
    AiVariableScope Scope,
    long? ScopeId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiVariableCreateRequest(
    string Key,
    string? Value,
    AiVariableScope Scope,
    long? ScopeId);

public sealed record AiVariableUpdateRequest(
    string Key,
    string? Value,
    AiVariableScope Scope,
    long? ScopeId);

public sealed record AiSystemVariableDefinition(
    string Key,
    string Name,
    string Description,
    string? DefaultValue);
