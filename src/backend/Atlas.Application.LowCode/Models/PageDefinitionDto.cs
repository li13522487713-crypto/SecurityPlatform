namespace Atlas.Application.LowCode.Models;

/// <summary>页面列表项（不含完整 schema）。</summary>
public sealed record PageDefinitionListItem(
    string Id,
    string AppId,
    string Code,
    string DisplayName,
    string Path,
    string TargetType,
    string Layout,
    int OrderNo,
    bool IsVisible,
    bool IsLocked,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>页面详情（含完整 schema JSON）。</summary>
public sealed record PageDefinitionDetail(
    string Id,
    string AppId,
    string Code,
    string DisplayName,
    string Path,
    string TargetType,
    string Layout,
    int OrderNo,
    bool IsVisible,
    bool IsLocked,
    string SchemaJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>创建页面请求。</summary>
public sealed record PageDefinitionCreateRequest(
    string Code,
    string DisplayName,
    string Path,
    string? TargetType,
    string? Layout);

/// <summary>更新页面元数据请求。</summary>
public sealed record PageDefinitionUpdateRequest(
    string DisplayName,
    string Path,
    string TargetType,
    string Layout,
    bool IsVisible,
    bool IsLocked);

/// <summary>替换页面 schema 请求（完整 PageSchema JSON）。</summary>
public sealed record PageSchemaReplaceRequest(string SchemaJson);

/// <summary>批量排序请求项。</summary>
public sealed record PageReorderItem(string Id, int OrderNo);

/// <summary>批量排序请求。</summary>
public sealed record PagesReorderRequest(IReadOnlyList<PageReorderItem> Items);
