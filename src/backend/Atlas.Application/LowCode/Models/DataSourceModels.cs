namespace Atlas.Application.LowCode.Models;

public sealed record DataSourceListItem(string Id, string Name, string SourceType, string? Description, int? CacheSeconds, DateTimeOffset CreatedAt);
public sealed record DataSourceDetail(string Id, string Name, string SourceType, string ConfigJson, string? Description, int? CacheSeconds, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record DataSourceCreateRequest(string Name, string SourceType, string ConfigJson, string? Description, int? CacheSeconds);
public sealed record DataSourceUpdateRequest(string Name, string SourceType, string ConfigJson, string? Description, int? CacheSeconds);
public sealed record DataSourceQueryRequest(string DataSourceId, string? ParametersJson);
public sealed record DataSourceQueryResult(bool Success, string? ErrorMessage, object? Data, long ElapsedMs);
