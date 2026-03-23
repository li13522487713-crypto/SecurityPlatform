namespace Atlas.Application.AiPlatform.Models;

public sealed record OpenApiProjectCreateRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt);

public sealed record OpenApiProjectUpdateRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Scopes,
    bool IsActive,
    DateTimeOffset? ExpiresAt);

public sealed record OpenApiProjectListItem(
    long Id,
    string Name,
    string Description,
    string AppId,
    string SecretPrefix,
    IReadOnlyList<string> Scopes,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    long CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OpenApiProjectCreateResult(
    long Id,
    string Name,
    string AppId,
    string AppSecret,
    string SecretPrefix,
    IReadOnlyList<string> Scopes,
    DateTime? ExpiresAt);

public sealed record OpenApiProjectRotateSecretResult(
    long Id,
    string AppId,
    string AppSecret,
    string SecretPrefix);

public sealed record OpenApiProjectTokenExchangeRequest(
    string AppId,
    string AppSecret);

public sealed record OpenApiProjectTokenExchangeResult(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    long ProjectId,
    string AppId,
    IReadOnlyList<string> Scopes);
