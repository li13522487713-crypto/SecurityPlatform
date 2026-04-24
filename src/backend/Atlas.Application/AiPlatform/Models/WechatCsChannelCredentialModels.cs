using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.AiPlatform.Models;

public sealed record WechatCsChannelCredentialDto(
    string Id,
    string ChannelId,
    string WorkspaceId,
    string CorpId,
    string CorpIdMasked,
    string OpenKfId,
    string Token,
    bool HasEncodingAesKey,
    DateTimeOffset? AccessTokenExpiresAt,
    long RefreshCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WechatCsChannelCredentialUpsertRequest(
    [Required, StringLength(64, MinimumLength = 1)] string CorpId,
    [Required, StringLength(128, MinimumLength = 1)] string Secret,
    [Required, StringLength(64, MinimumLength = 1)] string OpenKfId,
    [StringLength(64)] string? Token,
    [StringLength(128)] string? EncodingAesKey);
