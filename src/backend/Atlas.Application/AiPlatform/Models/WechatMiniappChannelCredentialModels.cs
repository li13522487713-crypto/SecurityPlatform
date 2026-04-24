using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.AiPlatform.Models;

public sealed record WechatMiniappChannelCredentialDto(
    string Id,
    string ChannelId,
    string WorkspaceId,
    string AppId,
    string AppIdMasked,
    string OriginalId,
    string MessageToken,
    bool HasEncodingAesKey,
    DateTimeOffset? AccessTokenExpiresAt,
    long RefreshCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WechatMiniappChannelCredentialUpsertRequest(
    [Required, StringLength(64, MinimumLength = 1)] string AppId,
    [Required, StringLength(128, MinimumLength = 1)] string AppSecret,
    [StringLength(64)] string? OriginalId,
    [StringLength(64)] string? MessageToken,
    [StringLength(128)] string? EncodingAesKey);
