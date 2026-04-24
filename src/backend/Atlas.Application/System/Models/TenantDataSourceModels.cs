namespace Atlas.Application.System.Models;

public sealed record TenantDataSourceDto(
    string Id,
    string TenantIdValue,
    string Name,
    string DbType,
    string DriverCode,
    string? Host,
    int? Port,
    string? DatabaseName,
    string? MaskedConnectionSummary,
    string OwnershipScope,
    string? OwnerAppInstanceId,
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
    string OwnershipScope = TenantDataSourceOwnershipScopes.Platform,
    string? OwnerAppInstanceId = null,
    string? AppId = null,
    string Mode = "raw",
    Dictionary<string, string>? VisualConfig = null,
    int MaxPoolSize = 50,
    int ConnectionTimeoutSeconds = 15);

public sealed record TenantDataSourceUpdateRequest(
    string Name,
    string? ConnectionString,
    string DbType,
    string? OwnershipScope = null,
    string? OwnerAppInstanceId = null,
    string? AppId = null,
    string Mode = "raw",
    Dictionary<string, string>? VisualConfig = null,
    int MaxPoolSize = 50,
    int ConnectionTimeoutSeconds = 15);

public sealed record TestConnectionRequest(
    string ConnectionString,
    string DbType = "SQLite",
    string Mode = "raw",
    Dictionary<string, string>? VisualConfig = null);

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

public sealed record DataSourceDriverFieldDefinition(
    string Key,
    string Label,
    string InputType,
    bool Required,
    bool Secret,
    bool Multiline,
    string? Placeholder,
    string? DefaultValue);

public sealed record DataSourceDriverDefinition(
    string Code,
    string DisplayName,
    bool SupportsVisual,
    string ConnectionStringExample,
    IReadOnlyList<DataSourceDriverFieldDefinition> Fields);

public static class TenantDataSourceOwnershipScopes
{
    public const string Platform = "Platform";
    public const string AppScoped = "AppScoped";
}
