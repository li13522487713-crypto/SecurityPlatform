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

public sealed record DynamicViewSqlPreviewRequest(
    DynamicViewCreateOrUpdateRequest Definition);

public sealed record DynamicViewSqlPreviewDto(
    string Sql,
    IReadOnlyList<DynamicSqlParameterDto> Parameters,
    IReadOnlyList<string> Warnings,
    bool FullyPushdown);

public sealed record DynamicSqlParameterDto(string Name, object? Value);

public sealed record DynamicJoinPlanDto(
    string JoinType,
    string LeftSource,
    string RightSource,
    IReadOnlyList<DynamicJoinConditionDto> Conditions);

public sealed record DynamicJoinConditionDto(string LeftField, string RightField);

public sealed record DynamicAggregatePlanDto(
    IReadOnlyList<string> GroupBy,
    IReadOnlyList<DynamicAggregateItemDto> Aggregates);

public sealed record DynamicAggregateItemDto(
    string Function,
    string? Field,
    string Alias);

public sealed record DynamicUnionPlanDto(
    string Mode,
    IReadOnlyList<string> Inputs);

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

public sealed record DynamicTransformJobDto(
    string Id,
    string? AppId,
    string JobKey,
    string Name,
    string Status,
    string? CronExpression,
    bool Enabled,
    DateTimeOffset? LastRunAt,
    string? LastRunStatus,
    string? LastError,
    string SourceConfigJson,
    string TargetConfigJson,
    string DefinitionJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DynamicTransformExecutionDto(
    string Id,
    string JobKey,
    string Status,
    string TriggerType,
    int InputRows,
    int OutputRows,
    int FailedRows,
    long DurationMs,
    string? ErrorDetailJson,
    long StartedBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? Message);

public sealed record DynamicTransformJobCreateRequest(
    string AppId,
    string JobKey,
    string Name,
    string DefinitionJson,
    string? CronExpression = null,
    bool Enabled = false,
    string? SourceConfigJson = null,
    string? TargetConfigJson = null);

public sealed record DynamicTransformJobUpdateRequest(
    string Name,
    string DefinitionJson,
    string? CronExpression,
    bool Enabled,
    string? SourceConfigJson = null,
    string? TargetConfigJson = null);

public sealed record DynamicExternalExtractPreviewRequest(
    long DataSourceId,
    string Sql,
    int Limit = 100);

public sealed record DynamicExternalExtractPreviewResult(
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<DynamicExternalExtractColumnDto> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows);

public sealed record DynamicExternalExtractColumnDto(
    string Name,
    string Type);

public sealed record DynamicExternalExtractDataSourceDto(
    string Id,
    string Name,
    string DbType);

public sealed record DynamicExternalExtractSchemaTableDto(
    string Name,
    IReadOnlyList<DynamicExternalExtractColumnDto> Columns);

public sealed record DynamicExternalExtractSchemaResult(
    string DataSourceId,
    IReadOnlyList<DynamicExternalExtractSchemaTableDto> Tables);

public sealed record DynamicPhysicalViewPublishRequest(
    bool ReplaceIfExists = true,
    string? PhysicalViewName = null,
    long? DataSourceId = null,
    string? Comment = null);

public sealed record DynamicPhysicalViewPublishResult(
    string ViewKey,
    string PublicationId,
    int Version,
    string PhysicalViewName,
    long? DataSourceId,
    string Status,
    DateTimeOffset PublishedAt,
    bool Success,
    string Message);

public sealed record DynamicPhysicalViewPublicationDto(
    string Id,
    string ViewKey,
    int Version,
    string PhysicalViewName,
    string Status,
    string? Comment,
    long? DataSourceId,
    long PublishedBy,
    DateTimeOffset PublishedAt);
