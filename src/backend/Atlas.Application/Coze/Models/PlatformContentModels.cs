using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Coze.Models;

/// <summary>
/// 平台运营内容 Slot 枚举（PRD 01 首页 + 02-7.9 社区 + 02-7.12 通用管理 + 模板/插件商店摘要）。
/// </summary>
public static class PlatformContentSlots
{
    public const string Banner = "banner";
    public const string Tutorial = "tutorial";
    public const string Announcement = "announcement";
    public const string Recommended = "recommended";
    public const string CommunityWork = "community-work";
    public const string PlatformNotice = "platform-notice";
    public const string MarketTemplateSummary = "market-template-summary";
    public const string MarketPluginSummary = "market-plugin-summary";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Banner,
        Tutorial,
        Announcement,
        Recommended,
        CommunityWork,
        PlatformNotice,
        MarketTemplateSummary,
        MarketPluginSummary
    };
}

public sealed record PlatformContentItemDto(
    string Id,
    string Slot,
    string ContentKey,
    string ContentJson,
    string? Tag,
    int OrderIndex,
    bool IsActive,
    DateTimeOffset PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PlatformContentCreateRequest(
    [Required, StringLength(32, MinimumLength = 1)] string Slot,
    [Required, StringLength(64, MinimumLength = 1)] string ContentKey,
    [Required] string ContentJson,
    [StringLength(32)] string? Tag,
    int OrderIndex,
    bool? IsActive,
    DateTimeOffset? PublishedAt);

public sealed record PlatformContentUpdateRequest(
    [Required] string ContentJson,
    [StringLength(32)] string? Tag,
    int OrderIndex,
    bool IsActive,
    DateTimeOffset? PublishedAt);
