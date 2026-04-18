namespace Atlas.Application.LowCode.Models;

/// <summary>应用变量列表项 / 详情（同结构）。</summary>
public sealed record AppVariableDto(
    string Id,
    string AppId,
    string Code,
    string DisplayName,
    string Scope,
    string ValueType,
    bool IsReadOnly,
    bool IsPersisted,
    string DefaultValueJson,
    string? ValidationJson,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>创建变量请求。</summary>
public sealed record AppVariableCreateRequest(
    string Code,
    string DisplayName,
    string Scope,
    string ValueType,
    bool IsPersisted,
    string DefaultValueJson,
    string? ValidationJson,
    string? Description);

/// <summary>更新变量请求。</summary>
public sealed record AppVariableUpdateRequest(
    string DisplayName,
    string ValueType,
    bool IsReadOnly,
    bool IsPersisted,
    string DefaultValueJson,
    string? ValidationJson,
    string? Description);
