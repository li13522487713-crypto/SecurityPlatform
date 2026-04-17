using System.Text.Json;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 持久化版本的工作空间首页内容（M4.5）。
///
/// 数据源：<see cref="PlatformContent"/>（按 Slot 区分 banner / tutorial / announcement / recommended）。
/// 当对应 Slot 无激活记录时 fallback 到内置默认内容，保证空数据库场景仍可用。
///
/// 后续运营 CRUD Controller 落地后，可直接通过 PATCH 更新 ContentJson 即可改首页。
/// </summary>
public sealed class PlatformHomeContentService : IHomeContentService
{
    private const string SlotBanner = "banner";
    private const string SlotTutorial = "tutorial";
    private const string SlotAnnouncement = "announcement";
    private const string SlotRecommended = "recommended";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HomeBannerDto DefaultBanner = new(
        HeroTitle: "扣子，让 AI 离应用更近一步",
        HeroSubtitle: "新一代 AI 应用开发平台 — 无需代码，轻松创建，支持发布多平台、WebSDK 及 API。",
        CtaList: new[]
        {
            new HomeBannerCtaDto("create", "立即创建"),
            new HomeBannerCtaDto("tutorial", "查看教程"),
            new HomeBannerCtaDto("docs", "查看文档")
        },
        BackgroundImageUrl: null);

    private static readonly IReadOnlyList<HomeTutorialCardDto> DefaultTutorials = new[]
    {
        new HomeTutorialCardDto("intro", "什么是扣子", "5 分钟了解平台基础概念。", "intro", "/docs/welcome"),
        new HomeTutorialCardDto("quickstart", "快速开始", "跟着指引创建你的第一个智能体。", "quickstart", "/docs/quick-start"),
        new HomeTutorialCardDto("release", "产品动态", "查看最新功能与版本更新。", "release", "/docs/release-notes")
    };

    private static readonly IReadOnlyList<HomeAnnouncementItemDto> DefaultAnnouncements = new[]
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

    private static readonly IReadOnlyList<HomeRecommendedAgentDto> DefaultRecommended = new[]
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

    private readonly PlatformContentRepository _repository;

    public PlatformHomeContentService(PlatformContentRepository repository)
    {
        _repository = repository;
    }

    public async Task<HomeBannerDto> GetBannerAsync(
        TenantId tenantId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(tenantId, SlotBanner, onlyActive: true, cancellationToken);
        if (rows.Count == 0)
        {
            return DefaultBanner;
        }

        return Deserialize<HomeBannerDto>(rows[0].ContentJson) ?? DefaultBanner;
    }

    public async Task<IReadOnlyList<HomeTutorialCardDto>> GetTutorialsAsync(
        TenantId tenantId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(tenantId, SlotTutorial, onlyActive: true, cancellationToken);
        if (rows.Count == 0)
        {
            return DefaultTutorials;
        }

        return rows
            .Select(row => Deserialize<HomeTutorialCardDto>(row.ContentJson))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    public async Task<PagedResult<HomeAnnouncementItemDto>> GetAnnouncementsAsync(
        TenantId tenantId,
        string workspaceId,
        HomeAnnouncementTab tab,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(tenantId, SlotAnnouncement, onlyActive: true, cancellationToken);
        IReadOnlyList<HomeAnnouncementItemDto> source;
        if (rows.Count == 0)
        {
            source = DefaultAnnouncements;
        }
        else
        {
            source = rows
                .Select(row => Deserialize<HomeAnnouncementItemDto>(row.ContentJson))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray();
        }

        var filtered = source
            .Where(item => tab != HomeAnnouncementTab.Notice || item.Tag == "公告")
            .Where(item => string.IsNullOrWhiteSpace(keyword)
                || item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);
        var skip = (pageIndex - 1) * pageSize;
        var page = filtered.Skip(skip).Take(pageSize).ToArray();

        return new PagedResult<HomeAnnouncementItemDto>(page, filtered.LongLength, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<HomeRecommendedAgentDto>> GetRecommendedAgentsAsync(
        TenantId tenantId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(tenantId, SlotRecommended, onlyActive: true, cancellationToken);
        if (rows.Count == 0)
        {
            return DefaultRecommended;
        }

        return rows
            .Select(row => Deserialize<HomeRecommendedAgentDto>(row.ContentJson))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    public Task<IReadOnlyList<HomeRecentActivityDto>> GetRecentActivitiesAsync(
        TenantId tenantId,
        string workspaceId,
        long currentUserId,
        CancellationToken cancellationToken)
    {
        // 第二轮接 WorkspaceIdeService 的 RecordActivity 历史记录；当前阶段保留空数组。
        IReadOnlyList<HomeRecentActivityDto> empty = Array.Empty<HomeRecentActivityDto>();
        return Task.FromResult(empty);
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
