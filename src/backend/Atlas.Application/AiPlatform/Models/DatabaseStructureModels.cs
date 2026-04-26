using Atlas.Domain.AiPlatform.Entities;
using System.Text.Json.Serialization;

namespace Atlas.Application.AiPlatform.Models;

public sealed record DatabaseObjectDto(
    string Name,
    string ObjectType,
    string? Schema,
    string? Engine,
    string? Algorithm,
    long? RowCount,
    string? Comment,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    string? Status = null,
    bool CanPreview = true,
    bool CanDrop = true);

public sealed record DatabaseColumnDto(
    string Name,
    string DataType,
    string? RawDataType,
    int? Length,
    int? Precision,
    int? Scale,
    bool Nullable,
    bool PrimaryKey,
    bool AutoIncrement,
    string? DefaultValue,
    string? Comment,
    int Ordinal);

public sealed record PreviewDataRequest(
    string? Schema,
    int PageIndex = 1,
    int PageSize = 20,
    string? OrderBy = null,
    IReadOnlyDictionary<string, string?>? Filters = null,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft);

public sealed record PreviewDataResponse(
    IReadOnlyList<PreviewDataColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    long Total,
    int PageIndex,
    int PageSize,
    bool Truncated = false,
    long ElapsedMs = 0);

public sealed record PreviewDataColumn(string Name, string DataType);

public sealed record DdlResponse(string Ddl);

public sealed record TableColumnDesignDto(
    string? Id,
    string Name,
    string DataType,
    int? Length = null,
    int? Precision = null,
    int? Scale = null,
    bool Nullable = true,
    bool PrimaryKey = false,
    bool AutoIncrement = false,
    string? DefaultValue = null,
    string? Comment = null)
{
    [JsonConstructor]
    public TableColumnDesignDto(
        string Name,
        string DataType,
        int? Length = null,
        int? Precision = null,
        int? Scale = null,
        bool Nullable = true,
        bool PrimaryKey = false,
        bool AutoIncrement = false,
        string? DefaultValue = null,
        string? Comment = null)
        : this(null, Name, DataType, Length, Precision, Scale, Nullable, PrimaryKey, AutoIncrement, DefaultValue, Comment)
    {
    }
}

public sealed record TableOptionsDto(
    string? Engine = null,
    string? Charset = null,
    string? Collation = null,
    string? Schema = null,
    string? Tablespace = null,
    bool IncludeAuditFields = false,
    IReadOnlyDictionary<string, string?>? ExtraOptions = null);

public sealed record PreviewCreateTableDdlRequest(
    string? Schema,
    string TableName,
    string? Comment,
    IReadOnlyList<TableColumnDesignDto> Columns,
    TableOptionsDto? Options = null);

public sealed record CreateTableRequest(
    string? Schema,
    string TableName,
    string? Comment,
    IReadOnlyList<TableColumnDesignDto> Columns,
    TableOptionsDto? Options = null,
    string Mode = "visual");

public sealed record CreateTableSqlRequest(string Sql);

public sealed record PreviewViewSqlRequest(
    string Sql,
    int Limit = 100,
    string? Schema = null,
    AiDatabaseRecordEnvironment Environment = AiDatabaseRecordEnvironment.Draft);

public sealed record CreateViewRequest(
    string? Schema,
    string ViewName,
    string? Comment,
    string Sql,
    string Mode = "SelectOnly");

public sealed record DropDatabaseObjectRequest(
    string? Schema,
    string ConfirmName,
    bool ConfirmDanger);
