namespace Atlas.Application.LowCode.Models;

/// <summary>聚合资源条目（M07 S07-3）。统一输出 id/name/updatedAt 三段式，供 Studio 资源面板"投射模式"渲染。</summary>
public sealed record AppResourceItem(
    string ResourceType,
    string Id,
    string Name,
    string? Description,
    DateTimeOffset? UpdatedAt);

public sealed record AppResourceCatalogDto(
    IReadOnlyDictionary<string, IReadOnlyList<AppResourceItem>> ByType,
    int Total);

public sealed record AppResourceQuery(
    /// <summary>逗号分隔的 resourceType 子集；为空则查全部已支持类型。</summary>
    string? Types,
    string? Keyword,
    int? PageIndex,
    int? PageSize);
