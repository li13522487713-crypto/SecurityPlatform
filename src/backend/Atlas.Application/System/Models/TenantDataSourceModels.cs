namespace Atlas.Application.System.Models;

public sealed record TenantDataSourceDto(
    string Id,
    string TenantIdValue,
    string Name,
    string DbType,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TenantDataSourceCreateRequest(
    string TenantIdValue,
    string Name,
    string ConnectionString,
    string DbType = "SQLite");

public sealed record TenantDataSourceUpdateRequest(
    string Name,
    string ConnectionString,
    string DbType);

public sealed record TestConnectionRequest(string ConnectionString, string DbType = "SQLite");

public sealed record TestConnectionResult(bool Success, string? ErrorMessage = null);
