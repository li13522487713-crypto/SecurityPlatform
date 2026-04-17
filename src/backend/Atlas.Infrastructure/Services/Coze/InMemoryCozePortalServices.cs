using System.Text.Json;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

// Coze PRD Phase III - M4.5 后：InMemoryHomeContentService 已被 PlatformHomeContentService
// 取代（基于 PlatformContent 表 + 内置默认数据 fallback）。原 in-memory 实现已删除。
// M5.5 后：Community / PlatformGeneral / MarketSummary 三个 Service 也统一升级为
// PlatformContent + fallback 模式，共用一张表。
//
// 命名保留 "InMemory*" 前缀以避免破坏现有注册；类行为已改为"持久化 + fallback"。
// 未来可安全重命名为 "PlatformBacked*" 前缀。

internal static class PlatformContentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static IReadOnlyList<T> MapRows<T>(IReadOnlyList<PlatformContent> rows, IReadOnlyList<T> fallback)
    {
        if (rows.Count == 0)
        {
            return fallback;
        }

        var mapped = rows
            .Select(row => Deserialize<T>(row.ContentJson))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();

        return mapped.Length == 0 ? fallback : mapped;
    }

    public static PagedResult<T> Paginate<T>(IEnumerable<T> source, PagedRequest request)
    {
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var list = source as IReadOnlyList<T> ?? source.ToArray();
        var skip = (pageIndex - 1) * pageSize;
        var page = list.Skip(skip).Take(pageSize).ToArray();
        return new PagedResult<T>(page, list.Count, pageIndex, pageSize);
    }
}

/// <summary>
/// 作品社区（PRD 02-7.9）。M5.5：读 PlatformContent Slot=community-work；空则 fallback。
/// </summary>
public sealed class InMemoryCommunityService : ICommunityService
{
    private static readonly IReadOnlyList<CommunityWorkItemDto> DefaultWorks = new[]
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

    private readonly PlatformContentRepository _repository;

    public InMemoryCommunityService(PlatformContentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<CommunityWorkItemDto>> ListWorksAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(
            tenantId,
            PlatformContentSlots.CommunityWork,
            onlyActive: true,
            cancellationToken);
        var source = PlatformContentJson.MapRows(rows, DefaultWorks);

        var filtered = source
            .Where(item => string.IsNullOrWhiteSpace(keyword)
                || item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return PlatformContentJson.Paginate(filtered, pagedRequest);
    }
}

/// <summary>
/// 通用管理（PRD 02-7.12）。M5.5 + M6.1：
/// - notices 读 PlatformContent Slot=platform-notice；空则 fallback
/// - branding 读 PlatformContent Slot=branding 取第一条 IsActive 记录；空则 fallback
/// </summary>
public sealed class InMemoryPlatformGeneralService : IPlatformGeneralService
{
    private static readonly IReadOnlyList<PlatformNoticeDto> DefaultNotices = new[]
    {
        new PlatformNoticeDto(
            "notice-maintenance",
            "系统例行维护通知",
            "本周日 02:00-04:00 将进行例行维护，可能短暂不可用。",
            "info",
            DateTimeOffset.UtcNow)
    };

    private static readonly PlatformBrandingDto DefaultBranding = new(
        LogoUrl: null,
        ProductName: "Atlas Coze",
        ProductSlogan: "你的 AI 应用开发伙伴");

    private readonly PlatformContentRepository _repository;

    public InMemoryPlatformGeneralService(PlatformContentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PlatformNoticeDto>> ListNoticesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(
            tenantId,
            PlatformContentSlots.PlatformNotice,
            onlyActive: true,
            cancellationToken);
        return PlatformContentJson.MapRows(rows, DefaultNotices);
    }

    public async Task<PlatformBrandingDto> GetBrandingAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(
            tenantId,
            PlatformContentSlots.Branding,
            onlyActive: true,
            cancellationToken);
        if (rows.Count == 0)
        {
            return DefaultBranding;
        }
        return PlatformContentJson.Deserialize<PlatformBrandingDto>(rows[0].ContentJson) ?? DefaultBranding;
    }
}

/// <summary>
/// 模板 / 插件商店分类摘要（PRD 02-7.7、7.8）。M5.5：读 PlatformContent，空则 fallback。
/// </summary>
public sealed class InMemoryMarketSummaryService : IMarketSummaryService
{
    private static readonly IReadOnlyList<MarketCategorySummaryDto> DefaultTemplates = new[]
    {
        new MarketCategorySummaryDto("agent", "智能体模板", 12, "客服/营销/咨询场景"),
        new MarketCategorySummaryDto("workflow", "工作流模板", 28, "RAG / 多轮问答 / 数据处理"),
        new MarketCategorySummaryDto("app", "应用模板", 5, "面向终端用户的应用模板")
    };

    private static readonly IReadOnlyList<MarketCategorySummaryDto> DefaultPlugins = new[]
    {
        new MarketCategorySummaryDto("search", "搜索类", 6, null),
        new MarketCategorySummaryDto("office", "办公类", 9, null),
        new MarketCategorySummaryDto("data", "数据类", 11, null)
    };

    private readonly PlatformContentRepository _repository;

    public InMemoryMarketSummaryService(PlatformContentRepository repository)
    {
        _repository = repository;
    }

    public Task<PagedResult<MarketCategorySummaryDto>> ListTemplateCategoriesAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
        => ListSummaryAsync(tenantId, PlatformContentSlots.MarketTemplateSummary, DefaultTemplates, keyword, pagedRequest, cancellationToken);

    public Task<PagedResult<MarketCategorySummaryDto>> ListPluginCategoriesAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
        => ListSummaryAsync(tenantId, PlatformContentSlots.MarketPluginSummary, DefaultPlugins, keyword, pagedRequest, cancellationToken);

    private async Task<PagedResult<MarketCategorySummaryDto>> ListSummaryAsync(
        TenantId tenantId,
        string slot,
        IReadOnlyList<MarketCategorySummaryDto> fallback,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.ListBySlotAsync(tenantId, slot, onlyActive: true, cancellationToken);
        var source = PlatformContentJson.MapRows(rows, fallback);

        var filtered = source
            .Where(item => string.IsNullOrWhiteSpace(keyword)
                || item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return PlatformContentJson.Paginate(filtered, pagedRequest);
    }
}

// Coze PRD Phase III - M6.3 后：InMemoryMeSettingsService 已被 PersistentMeSettingsService
// 取代（基于 UserSetting 表，跨进程保留）。原 in-memory 实现已删除。
