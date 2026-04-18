using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.AiPlatform.Models;

public sealed record WechatMpChannelCredentialDto(
    string Id,
    string ChannelId,
    string WorkspaceId,
    string AppId,
    string AppIdMasked,
    string Token,
    bool HasEncodingAesKey,
    DateTimeOffset? AccessTokenExpiresAt,
    long RefreshCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WechatMpChannelCredentialUpsertRequest(
    [Required, StringLength(64, MinimumLength = 1)] string AppId,
    [Required, StringLength(128, MinimumLength = 1)] string AppSecret,
    [Required, StringLength(64, MinimumLength = 1)] string Token,
    [StringLength(128)] string? EncodingAesKey);
