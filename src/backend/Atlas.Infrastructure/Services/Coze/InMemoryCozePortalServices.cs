using System.Collections.Concurrent;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// Coze PRD 第一阶段平台/个人级 in-memory 实现。
///
/// 设计取舍：
/// - 这些数据原型是“运营内容 + 用户级偏好”，不需要立即持久化。第一阶段仅满足前端协议，
///   后续 M2 / M3 可逐项升级为 SqlSugar 表（PlatformContent / MeSettings 等）。
/// - 全部注册为 Singleton，无状态部分直接返回常量；MeSettings 用 ConcurrentDictionary 模拟用户偏好。
/// </summary>
public sealed class InMemoryHomeContentService : IHomeContentService
{
    private static readonly HomeBannerDto Banner = new(
        HeroTitle: "扣子，让 AI 离应用更近一步",
        HeroSubtitle: "新一代 AI 应用开发平台 — 无需代码，轻松创建，支持发布多平台、WebSDK 及 API。",
        CtaList: new[]
        {
            new HomeBannerCtaDto("create", "立即创建"),
            new HomeBannerCtaDto("tutorial", "查看教程"),
            new HomeBannerCtaDto("docs", "查看文档")
        },
        BackgroundImageUrl: null);

    private static readonly IReadOnlyList<HomeTutorialCardDto> Tutorials = new[]
    {
        new HomeTutorialCardDto("intro", "什么是扣子", "5 分钟了解平台基础概念。", "intro", "/docs/welcome"),
        new HomeTutorialCardDto("quickstart", "快速开始", "跟着指引创建你的第一个智能体。", "quickstart", "/docs/quick-start"),
        new HomeTutorialCardDto("release", "产品动态", "查看最新功能与版本更新。", "release", "/docs/release-notes")
    };

    private static readonly IReadOnlyList<HomeAnnouncementItemDto> Announcements = new[]
    {
        new HomeAnnouncementItemDto(
            "ann-1",
            "扣子小助手工作流模板已上线",
            "官方模板帮助你快速搭建客服 / 销售助手。",
            "扣子官方",
            DateTimeOffset.Parse("2026-04-12T10:00:00Z"),
            "公告",
            "/docs/release-notes#tpl"),
        new HomeAnnouncementItemDto(
            "ann-2",
            "DAG 工作流引擎升级：支持批处理与续跑",
            "引擎能力对齐 Coze parity，新节点支持续跑能力。",
            "工作流团队",
            DateTimeOffset.Parse("2026-04-10T08:00:00Z"),
            null,
            "/docs/release-notes#dag"),
        new HomeAnnouncementItemDto(
            "ann-3",
            "新版资源库已上线",
            "知识库/插件/数据库统一入口，支持工作空间维度复用。",
            "平台团队",
            DateTimeOffset.Parse("2026-04-05T08:00:00Z"),
            "公告",
            "/docs/release-notes#library")
    };

    private static readonly IReadOnlyList<HomeRecommendedAgentDto> Recommended = new[]
    {
        new HomeRecommendedAgentDto(
            "rec-1",
            "秒剪短视频",
            "一站式视频脚本生成助手。",
            null,
            "淘宝智能型",
            220_000,
            1_600,
            "/agent/rec-1/editor"),
        new HomeRecommendedAgentDto(
            "rec-2",
            "华泰股市助手",
            "实时行情解读 + 投研助手。",
            null,
            "华泰证券",
            158_000,
            980,
            "/agent/rec-2/editor")
    };

    public Task<HomeBannerDto> GetBannerAsync(TenantId tenantId, string workspaceId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Banner);
    }

    public Task<IReadOnlyList<HomeTutorialCardDto>> GetTutorialsAsync(
        TenantId tenantId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Tutorials);
    }

    public Task<PagedResult<HomeAnnouncementItemDto>> GetAnnouncementsAsync(
        TenantId tenantId,
        string workspaceId,
        HomeAnnouncementTab tab,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var filtered = Announcements
            .Where(item => tab != HomeAnnouncementTab.Notice || item.Tag == "公告")
            .Where(item => string.IsNullOrWhiteSpace(keyword)
                || item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);
        var skip = (pageIndex - 1) * pageSize;
        var page = filtered.Skip(skip).Take(pageSize).ToArray();

        return Task.FromResult(new PagedResult<HomeAnnouncementItemDto>(
            page,
            filtered.LongLength,
            pageIndex,
            pageSize));
    }

    public Task<IReadOnlyList<HomeRecommendedAgentDto>> GetRecommendedAgentsAsync(
        TenantId tenantId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Recommended);
    }

    public Task<IReadOnlyList<HomeRecentActivityDto>> GetRecentActivitiesAsync(
        TenantId tenantId,
        string workspaceId,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        // 第一阶段返回空数组，鼓励前端走"空状态 + 创建引导"。
        // 第二阶段对接 WorkspaceIdeService 的 RecordActivity 历史记录。
        IReadOnlyList<HomeRecentActivityDto> empty = Array.Empty<HomeRecentActivityDto>();
        return Task.FromResult(empty);
    }
}

public sealed class InMemoryCommunityService : ICommunityService
{
    private static readonly IReadOnlyList<CommunityWorkItemDto> Works = new[]
    {
        new CommunityWorkItemDto(
            "work-1",
            "客服小助手最佳实践",
            "如何用扣子搭建一个金融行业客服智能体。",
            "扣子官方",
            null,
            1234,
            45_678,
            DateTimeOffset.Parse("2026-04-10T10:00:00Z"),
            new[] { "客服", "金融" }),
        new CommunityWorkItemDto(
            "work-2",
            "RAG 知识库实战",
            "从 0 到 1 搭建一个企业知识检索智能体。",
            "社区作者 Alex",
            null,
            856,
            23_456,
            DateTimeOffset.Parse("2026-04-05T08:00:00Z"),
            new[] { "RAG", "知识库" })
    };

    public Task<PagedResult<CommunityWorkItemDto>> ListWorksAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var filtered = Works
            .Where(item => string.IsNullOrWhiteSpace(keyword)
                || item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);
        var skip = (pageIndex - 1) * pageSize;
        var page = filtered.Skip(skip).Take(pageSize).ToArray();

        return Task.FromResult(new PagedResult<CommunityWorkItemDto>(
            page,
            filtered.LongLength,
            pageIndex,
            pageSize));
    }
}

public sealed class InMemoryPlatformGeneralService : IPlatformGeneralService
{
    private static readonly IReadOnlyList<PlatformNoticeDto> Notices = new[]
    {
        new PlatformNoticeDto(
            "notice-maintenance",
            "系统例行维护通知",
            "本周日 02:00-04:00 将进行例行维护，可能短暂不可用。",
            "info",
            DateTimeOffset.UtcNow)
    };

    private static readonly PlatformBrandingDto Branding = new(
        LogoUrl: null,
        ProductName: "Atlas Coze",
        ProductSlogan: "你的 AI 应用开发伙伴");

    public Task<IReadOnlyList<PlatformNoticeDto>> ListNoticesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Notices);
    }

    public Task<PlatformBrandingDto> GetBrandingAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Branding);
    }
}

public sealed class InMemoryMarketSummaryService : IMarketSummaryService
{
    private static readonly IReadOnlyList<MarketCategorySummaryDto> Templates = new[]
    {
        new MarketCategorySummaryDto("agent", "智能体模板", 12, "客服/营销/咨询场景"),
        new MarketCategorySummaryDto("workflow", "工作流模板", 28, "RAG / 多轮问答 / 数据处理"),
        new MarketCategorySummaryDto("app", "应用模板", 5, "面向终端用户的应用模板")
    };

    private static readonly IReadOnlyList<MarketCategorySummaryDto> Plugins = new[]
    {
        new MarketCategorySummaryDto("search", "搜索类", 6, null),
        new MarketCategorySummaryDto("office", "办公类", 9, null),
        new MarketCategorySummaryDto("data", "数据类", 11, null)
    };

    public Task<PagedResult<MarketCategorySummaryDto>> ListTemplateCategoriesAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
        => PaginateAsync(Templates, keyword, pagedRequest);

    public Task<PagedResult<MarketCategorySummaryDto>> ListPluginCategoriesAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
        => PaginateAsync(Plugins, keyword, pagedRequest);

    private static Task<PagedResult<MarketCategorySummaryDto>> PaginateAsync(
        IReadOnlyList<MarketCategorySummaryDto> source,
        string? keyword,
        PagedRequest pagedRequest)
    {
        var filtered = source
            .Where(item => string.IsNullOrWhiteSpace(keyword)
                || item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);
        var skip = (pageIndex - 1) * pageSize;
        var page = filtered.Skip(skip).Take(pageSize).ToArray();

        return Task.FromResult(new PagedResult<MarketCategorySummaryDto>(
            page,
            filtered.LongLength,
            pageIndex,
            pageSize));
    }
}

public sealed class InMemoryMeSettingsService : IMeSettingsService
{
    private static readonly ConcurrentDictionary<string, MeGeneralSettingsDto> GeneralSettings = new();

    private static readonly IReadOnlyList<MePublishChannelDto> DefaultChannels = new[]
    {
        new MePublishChannelDto("ch-wechat-personal", "微信个人", "wechat-personal", false),
        new MePublishChannelDto("ch-feishu-personal", "飞书个人", "feishu-personal", false)
    };

    private static readonly IReadOnlyList<MeDataSourceDto> DefaultDataSources = new[]
    {
        new MeDataSourceDto("ds-qdrant", "默认 Qdrant", "qdrant", true),
        new MeDataSourceDto("ds-minio", "默认 MinIO", "minio", true)
    };

    public Task<MeGeneralSettingsDto> GetGeneralAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(tenantId, currentUser);
        var settings = GeneralSettings.GetOrAdd(key, _ => new MeGeneralSettingsDto("zh-CN", "light", null));
        return Task.FromResult(settings);
    }

    public Task<MeGeneralSettingsDto> UpdateGeneralAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        MeGeneralSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(tenantId, currentUser);
        var updated = GeneralSettings.AddOrUpdate(
            key,
            _ => Apply(new MeGeneralSettingsDto("zh-CN", "light", null), request),
            (_, existing) => Apply(existing, request));
        return Task.FromResult(updated);
    }

    public Task<IReadOnlyList<MePublishChannelDto>> ListPublishChannelsAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(DefaultChannels);
    }

    public Task<IReadOnlyList<MeDataSourceDto>> ListDataSourcesAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(DefaultDataSources);
    }

    public Task DeleteAccountAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken)
    {
        // 第一阶段不真正删除账号；仅清空个人偏好（占位接口）。
        var key = BuildKey(tenantId, currentUser);
        GeneralSettings.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private static string BuildKey(TenantId tenantId, CurrentUserInfo currentUser)
    {
        return $"{tenantId.Value:N}:{currentUser.UserId}";
    }

    private static MeGeneralSettingsDto Apply(MeGeneralSettingsDto current, MeGeneralSettingsUpdateRequest patch)
    {
        return new MeGeneralSettingsDto(
            Locale: NormalizeLocale(patch.Locale ?? current.Locale),
            Theme: NormalizeTheme(patch.Theme ?? current.Theme),
            DefaultWorkspaceId: patch.DefaultWorkspaceId ?? current.DefaultWorkspaceId);
    }

    private static string NormalizeLocale(string locale)
    {
        return locale switch
        {
            "zh-CN" or "en-US" => locale,
            _ => "zh-CN"
        };
    }

    private static string NormalizeTheme(string theme)
    {
        return theme switch
        {
            "light" or "dark" or "system" => theme,
            _ => "light"
        };
    }
}
