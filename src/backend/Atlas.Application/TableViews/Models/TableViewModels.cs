using System.Text.Json;

namespace Atlas.Application.TableViews.Models;

public sealed record TableViewListItem(
    string Id,
    string Name,
    string TableKey,
    int ConfigVersion,
    bool IsDefault,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt);

public sealed record TableViewDetail(
    string Id,
    string Name,
    string TableKey,
    int ConfigVersion,
    bool IsDefault,
    TableViewConfig Config,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt);

public sealed record TableViewCreateRequest(
    string TableKey,
    string Name,
    TableViewConfig Config,
    int? ConfigVersion);

public sealed record TableViewUpdateRequest(
    string Name,
    TableViewConfig Config,
    int? ConfigVersion);

public sealed record TableViewConfigUpdateRequest(
    TableViewConfig Config,
    int? ConfigVersion);

public sealed record TableViewDuplicateRequest(string Name);

public sealed class TableViewConfig
{
    public IReadOnlyList<TableViewColumnConfig> Columns { get; init; } = Array.Empty<TableViewColumnConfig>();
    public string? Density { get; init; }
    public TableViewPagination? Pagination { get; init; }
    public IReadOnlyList<TableViewSort> Sort { get; init; } = Array.Empty<TableViewSort>();
    public IReadOnlyList<TableViewFilter> Filters { get; init; } = Array.Empty<TableViewFilter>();
    public TableViewGroupBy? GroupBy { get; init; }
    public IReadOnlyList<TableViewAggregation> Aggregations { get; init; } = Array.Empty<TableViewAggregation>();
    public TableViewQueryPanel? QueryPanel { get; init; }
    public TableViewQueryGroup? QueryModel { get; init; }
    public IReadOnlyList<MergeCellRule> MergeCells { get; init; } = Array.Empty<MergeCellRule>();
}

public sealed class TableViewColumnConfig
{
    public string Key { get; init; } = string.Empty;
    public bool Visible { get; init; } = true;
    public int Order { get; init; }
    public int? Width { get; init; }
    public string? Pinned { get; init; }
    public string? Align { get; init; }
    public bool? Ellipsis { get; init; }
    public bool? Wrap { get; init; }
    public bool? Tooltip { get; init; }
}

public sealed class TableViewPagination
{
    public int PageSize { get; init; }
}

public sealed class TableViewSort
{
    public string Key { get; init; } = string.Empty;
    public string Order { get; init; } = string.Empty;
    public int Priority { get; init; }
}

public sealed class TableViewFilter
{
    public string Key { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public JsonElement? Value { get; init; }
}

public sealed class TableViewGroupBy
{
    public string Key { get; init; } = string.Empty;
    public IReadOnlyList<string> CollapsedKeys { get; init; } = Array.Empty<string>();
}

public sealed class TableViewAggregation
{
    public string Key { get; init; } = string.Empty;
    public string Op { get; init; } = string.Empty;
}

public sealed class TableViewQueryPanel
{
    public bool Open { get; init; }
    public bool AutoSearch { get; init; }
    public string? SavedFilterId { get; init; }
}

public sealed class TableViewQueryGroup
{
    public string Logic { get; init; } = "AND";
    public IReadOnlyList<TableViewQueryCondition> Conditions { get; init; } = Array.Empty<TableViewQueryCondition>();
    public IReadOnlyList<TableViewQueryGroup> Groups { get; init; } = Array.Empty<TableViewQueryGroup>();
}

public sealed class TableViewQueryCondition
{
    public string Field { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public JsonElement? Value { get; init; }
}

/// <summary>
/// 行合并规则：对 columnKey 列的相邻相同值自动合并行（rowSpan）。
/// dependsOn 指定依赖列，只有依赖列也相同时才合并（支持多级分组合并）。
/// </summary>
public sealed class MergeCellRule
{
    public string ColumnKey { get; init; } = string.Empty;
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
}
