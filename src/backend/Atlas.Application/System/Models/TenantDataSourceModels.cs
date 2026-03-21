namespace Atlas.Application.System.Models;

public sealed record TenantDataSourceDto(
    string Id,
    string TenantIdValue,
    string Name,
    string DbType,
    string? AppId,
    int MaxPoolSize,
    int ConnectionTimeoutSeconds,
    bool? LastTestSuccess,
    DateTimeOffset? LastTestedAt,
    string? LastTestMessage,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TenantDataSourceCreateRequest(
    string TenantIdValue,
    string Name,
    string ConnectionString,
    string DbType = "SQLite",
    string? AppId = null,
    int MaxPoolSize = 50,
    int ConnectionTimeoutSeconds = 15);

public sealed record TenantDataSourceUpdateRequest(
    string Name,
    string? ConnectionString,
    string DbType,
    int MaxPoolSize = 50,
    int ConnectionTimeoutSeconds = 15);

public sealed record TestConnectionRequest(string ConnectionString, string DbType = "SQLite");

public sealed record TestConnectionResult(bool Success, string? ErrorMessage = null, int? LatencyMs = null);

public sealed record TenantDbConnectionInfo(string ConnectionString, string DbType);

public sealed record DataSourceConsumerItem(
    string BindingId,
    string AppInstanceId,
    string AppInstanceName,
    string BindingType,
    bool IsActive,
    DateTimeOffset BoundAt);

public sealed record DataSourceOrphanItem(
    string DataSourceId,
    string Name,
    string DbType,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastTestedAt);
