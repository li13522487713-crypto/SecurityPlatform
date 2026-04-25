namespace Atlas.Infrastructure.Services.DatabaseStructure;

public sealed record CreateTableDefinition(
    string? Schema,
    string TableName,
    string? Comment,
    IReadOnlyList<CreateTableColumnDefinition> Columns,
    CreateTableOptionsDefinition? Options,
    string DriverCode);

public sealed record CreateTableColumnDefinition(
    string Name,
    string DataType,
    int? Length,
    int? Precision,
    int? Scale,
    bool Nullable,
    bool PrimaryKey,
    bool AutoIncrement,
    string? DefaultValue,
    string? Comment,
    int Ordinal);

public sealed record CreateTableOptionsDefinition(
    string? Engine,
    string? Charset,
    string? Collation,
    string? Tablespace,
    string? Schema,
    bool IncludeAuditFields,
    IReadOnlyDictionary<string, string?>? ExtraOptions);

public sealed record CreateViewDefinition(
    string? Schema,
    string ViewName,
    string? Comment,
    string? SelectSql,
    string? CreateSql,
    CreateViewMode Mode);

public enum CreateViewMode
{
    SelectOnly = 0,
    CreateViewSql = 1
}
