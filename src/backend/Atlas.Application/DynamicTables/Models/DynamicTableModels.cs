using System.Text.Json;

namespace Atlas.Application.DynamicTables.Models;

public sealed record DynamicTableListItem(
    string Id,
    string TableKey,
    string DisplayName,
    string? Description,
    string DbType,
    string Status,
    DateTimeOffset CreatedAt,
    long CreatedBy,
    long? ApprovalFlowDefinitionId = null,
    string? ApprovalStatusField = null);

public sealed record DynamicTableDetail(
    string Id,
    string TableKey,
    string DisplayName,
    string? Description,
    string DbType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long CreatedBy,
    long UpdatedBy,
    IReadOnlyList<DynamicFieldDefinition> Fields,
    IReadOnlyList<DynamicIndexDefinition> Indexes,
    long? ApprovalFlowDefinitionId = null,
    string? ApprovalStatusField = null);

public sealed record DynamicTableCreateRequest(
    string TableKey,
    string DisplayName,
    string? Description,
    string DbType,
    IReadOnlyList<DynamicFieldDefinition> Fields,
    IReadOnlyList<DynamicIndexDefinition> Indexes);

public sealed record DynamicTableUpdateRequest(
    string DisplayName,
    string? Description,
    string Status);

public sealed record DynamicTableAlterRequest(
    IReadOnlyList<DynamicFieldDefinition> AddFields,
    IReadOnlyList<DynamicFieldUpdateDefinition> UpdateFields,
    IReadOnlyList<string> RemoveFields);

public sealed record DynamicFieldDefinition(
    string Name,
    string? DisplayName,
    string FieldType,
    int? Length,
    int? Precision,
    int? Scale,
    bool AllowNull,
    bool IsPrimaryKey,
    bool IsAutoIncrement,
    bool IsUnique,
    string? DefaultValue,
    int SortOrder);

public sealed record DynamicFieldUpdateDefinition(
    string Name,
    string? DisplayName,
    int? Length,
    int? Precision,
    int? Scale,
    bool? AllowNull,
    bool? IsUnique,
    string? DefaultValue,
    int? SortOrder);

public sealed record DynamicIndexDefinition(
    string Name,
    bool IsUnique,
    IReadOnlyList<string> Fields);

public sealed record DynamicRelationDefinition(
    string RelatedTableKey,
    string SourceField,
    string TargetField,
    string RelationType,
    string? CascadeRule);

public sealed record DynamicRelationUpsertRequest(
    IReadOnlyList<DynamicRelationDefinition> Relations);

public sealed record DynamicFieldTypeOption(string Label, string Value);

/// <summary>
/// 动态表绑定审批流请求
/// </summary>
public sealed record DynamicTableApprovalBindingRequest(
    long? ApprovalFlowDefinitionId,
    string? ApprovalStatusField);

/// <summary>
/// 从动态表记录发起审批的响应
/// </summary>
public sealed record DynamicTableApprovalSubmitResponse(
    string InstanceId,
    string RecordId,
    string Status);

public sealed record DynamicRecordUpsertRequest(IReadOnlyList<DynamicFieldValueDto> Values);

public sealed record DynamicRecordBatchDeleteRequest(IReadOnlyList<long> Ids);

public sealed record DynamicRecordQueryRequest(
    int PageIndex,
    int PageSize,
    string? Keyword,
    string? SortBy,
    bool SortDesc,
    IReadOnlyList<DynamicFilterCondition> Filters);

public sealed record DynamicFilterCondition(
    string Field,
    string Operator,
    JsonElement? Value);

public sealed record DynamicRecordDto(
    string Id,
    IReadOnlyList<DynamicFieldValueDto> Values);

public sealed record DynamicRecordListResult(
    IReadOnlyList<DynamicRecordDto> Items,
    int Total,
    int PageIndex,
    int PageSize,
    IReadOnlyList<DynamicColumnDef> Columns);

public sealed record DynamicColumnDef(
    string Name,
    string Label,
    string Type,
    bool Sortable,
    bool Searchable,
    bool QuickEdit);

public sealed record DynamicFieldValueDto
{
    public string Field { get; init; } = string.Empty;
    public string ValueType { get; init; } = string.Empty;
    public string? StringValue { get; init; }
    public int? IntValue { get; init; }
    public long? LongValue { get; init; }
    public decimal? DecimalValue { get; init; }
    public bool? BoolValue { get; init; }
    public DateTimeOffset? DateTimeValue { get; init; }
    public DateTimeOffset? DateValue { get; init; }
}
