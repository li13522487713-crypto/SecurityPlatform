using System.Collections.Concurrent;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Coze;

// Coze PRD Phase III - M4.5 后：InMemoryHomeContentService 已被 PlatformHomeContentService
// 取代（基于 PlatformContent 表 + 内置默认数据 fallback）。原 in-memory 实现已删除。

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
