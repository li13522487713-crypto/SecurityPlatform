using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.AiPlatform.Models;

/// <summary>
/// 治理 M-G02-C8：飞书渠道凭据 DTO。AppSecret/EncryptKey 在传输与存储中均为加密；返回时只暴露脱敏值。
/// </summary>
public sealed record FeishuChannelCredentialDto(
    string Id,
    string ChannelId,
    string WorkspaceId,
    string AppId,
    string AppIdMasked,
    string VerificationToken,
    bool HasEncryptKey,
    DateTimeOffset? TenantAccessTokenExpiresAt,
    long RefreshCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FeishuChannelCredentialUpsertRequest(
    [Required, StringLength(64, MinimumLength = 1)] string AppId,
    [Required, StringLength(128, MinimumLength = 1)] string AppSecret,
    [Required, StringLength(64, MinimumLength = 1)] string VerificationToken,
    [StringLength(128)] string? EncryptKey);
