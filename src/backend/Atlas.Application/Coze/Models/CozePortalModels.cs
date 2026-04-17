namespace Atlas.Application.Coze.Models;

/// <summary>
/// Coze PRD 第一阶段（首页 / 平台生态 / 个人设置 / 模板插件商店摘要）平台级 DTO。
/// 协议草案见 docs/mock-api-protocols.md，前端 mock 见 src/frontend/apps/app-web/src/services/mock/。
/// </summary>
public sealed record HomeBannerDto(
    string HeroTitle,
    string HeroSubtitle,
    IReadOnlyList<HomeBannerCtaDto> CtaList,
    string? BackgroundImageUrl);

public sealed record HomeBannerCtaDto(string Key, string Label);

public sealed record HomeTutorialCardDto(
    string Id,
    string Title,
    string Description,
    string IconKey,
    string Link);

public enum HomeAnnouncementTab
{
    All = 0,
    Notice = 1
}

public sealed record HomeAnnouncementItemDto(
    string Id,
    string Title,
    string Summary,
    string Publisher,
    DateTimeOffset PublishedAt,
    string? Tag,
    string Link);

public sealed record HomeRecommendedAgentDto(
    string Id,
    string Name,
    string Description,
    string? IconUrl,
    string PublisherName,
    long Views,
    long Likes,
    string Link);

public sealed record HomeRecentActivityDto(
    string Id,
    string Type,
    string Name,
    string? Description,
    DateTimeOffset UpdatedAt,
    string EntryRoute);

public sealed record CommunityWorkItemDto(
    string Id,
    string Title,
    string Summary,
    string AuthorDisplayName,
    string? CoverUrl,
    long Likes,
    long Views,
    DateTimeOffset PublishedAt,
    IReadOnlyList<string> Tags);

public sealed record PlatformNoticeDto(
    string Id,
    string Title,
    string Message,
    string Level,
    DateTimeOffset PublishedAt);

public sealed record PlatformBrandingDto(
    string? LogoUrl,
    string ProductName,
    string ProductSlogan);

public sealed record MarketCategorySummaryDto(
    string Id,
    string Name,
    long Count,
    string? Description);

public sealed record MeGeneralSettingsDto(
    string Locale,
    string Theme,
    string? DefaultWorkspaceId);

public sealed record MeGeneralSettingsUpdateRequest(
    string? Locale,
    string? Theme,
    string? DefaultWorkspaceId);

public sealed record MePublishChannelDto(
    string Id,
    string Name,
    string Type,
    bool Bound);

public sealed record MeDataSourceDto(
    string Id,
    string Name,
    string Type,
    bool Bound);
