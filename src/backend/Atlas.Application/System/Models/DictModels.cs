namespace Atlas.Application.System.Models;

// ── 字典类型 ────────────────────────────────────────────────────────────────

public sealed record DictTypeDto(
    long Id,
    string Code,
    string Name,
    bool Status,
    string? Remark);

public sealed record DictTypeCreateRequest(
    string Code,
    string Name,
    bool Status,
    string? Remark);

public sealed record DictTypeUpdateRequest(
    string Name,
    bool Status,
    string? Remark);

// ── 字典数据 ────────────────────────────────────────────────────────────────

public sealed record DictDataDto(
    long Id,
    string DictTypeCode,
    string Label,
    string Value,
    int SortOrder,
    bool Status,
    string? CssClass,
    string? ListClass);

public sealed record DictDataCreateRequest(
    string Label,
    string Value,
    int SortOrder,
    bool Status,
    string? CssClass,
    string? ListClass);

public sealed record DictDataUpdateRequest(
    string Label,
    string Value,
    int SortOrder,
    bool Status,
    string? CssClass,
    string? ListClass);
