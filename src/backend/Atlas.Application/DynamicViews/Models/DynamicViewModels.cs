using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Models;

namespace Atlas.Application.DynamicViews.Models;

public sealed record DynamicViewListItem(
    string Id,
    string? AppId,
    string ViewKey,
    string Name,
    string? Description,
    bool IsPublished,
    int PublishedVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long CreatedBy,
    long UpdatedBy);

public sealed record DynamicViewDefinitionDto(
    string? Id,
    string AppId,
    string ViewKey,
    string Name,
    string? Description,
    IReadOnlyList<DynamicViewNodeDto> Nodes,
    IReadOnlyList<DynamicViewEdgeDto> Edges,
    IReadOnlyList<DynamicViewOutputFieldDto> OutputFields,
    IReadOnlyList<JsonFilterRuleDto>? Filters,
    IReadOnlyList<string>? GroupBy,
    IReadOnlyList<DynamicViewSortDto>? Sorts);

public sealed record DynamicViewNodeDto(
    string Id,
    string Type,
    string? Label,
    string? TableKey,
    string? ViewKey,
    object? Config);

public sealed record DynamicViewEdgeDto(
    string Id,
    string SourceNodeId,
    string? SourcePortId,
    string TargetNodeId,
    string? TargetPortId);

public sealed record DynamicViewOutputFieldDto(
    string TargetFieldKey,
    string TargetLabel,
    string TargetType,
    bool? Nullable,
    DynamicViewFieldSourceDto? Source,
    IReadOnlyList<DynamicViewTransformOpDto> Pipeline,
    string? OnError);

public sealed record DynamicViewFieldSourceDto(string NodeId, string FieldKey);

public sealed record DynamicViewTransformOpDto(string Type, Dictionary<string, object?>? Args);

public sealed record DynamicViewSortDto(string Field, string Direction);

public sealed record JsonFilterRuleDto(string Field, string Operator, object? Value);

public sealed record DynamicViewCreateOrUpdateRequest(
    string AppId,
    string ViewKey,
    string Name,
    string? Description,
    IReadOnlyList<DynamicViewNodeDto> Nodes,
    IReadOnlyList<DynamicViewEdgeDto> Edges,
    IReadOnlyList<DynamicViewOutputFieldDto> OutputFields,
    IReadOnlyList<JsonFilterRuleDto>? Filters,
    IReadOnlyList<string>? GroupBy,
    IReadOnlyList<DynamicViewSortDto>? Sorts);

public sealed record DynamicViewPreviewRequest(
    DynamicViewCreateOrUpdateRequest Definition,
    int? Limit = null);

public sealed record DynamicViewHistoryItemDto(
    int Version,
    string Status,
    long CreatedBy,
    DateTimeOffset CreatedAt,
    string? Comment,
    string Checksum);

public sealed record DynamicViewPublishResultDto(
    string ViewKey,
    int Version,
    DateTimeOffset PublishedAt,
    string Checksum);

public sealed record DeleteCheckBlockerDto(
    string Type,
    string Id,
    string Name,
    string? Path = null);

public sealed record DeleteCheckResultDto(
    bool CanDelete,
    IReadOnlyList<DeleteCheckBlockerDto> Blockers,
    IReadOnlyList<string> Warnings);

public sealed record DynamicViewDeleteCheckResult(
    string ViewKey,
    DeleteCheckResultDto Result);

public sealed record DynamicTableDeleteCheckResult(
    string TableKey,
    DeleteCheckResultDto Result);

public sealed record DynamicViewRecordsQueryRequest(
    int PageIndex,
    int PageSize,
    string? Keyword,
    string? SortBy,
    bool SortDesc,
    IReadOnlyList<DynamicFilterCondition>? Filters)
{
    public AdvancedQueryConfig? AdvancedQuery { get; init; }
}
