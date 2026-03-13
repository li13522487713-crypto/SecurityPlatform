namespace Atlas.Application.AiPlatform.Models;

public sealed record PersonalAccessTokenListItem(
    long Id,
    string Name,
    string TokenPrefix,
    IReadOnlyList<string> Scopes,
    long CreatedByUserId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    DateTime CreatedAt);

public sealed record PersonalAccessTokenCreateRequest(
    string Name,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt);

public sealed record PersonalAccessTokenCreateResult(
    long Id,
    string Name,
    string TokenPrefix,
    string PlainTextToken,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> Scopes);

public sealed record PersonalAccessTokenUpdateRequest(
    string Name,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt);

public sealed record PersonalAccessTokenValidateResult(
    bool Success,
    long TokenId,
    long UserId,
    IReadOnlyList<string> Scopes,
    string FailureReason);
