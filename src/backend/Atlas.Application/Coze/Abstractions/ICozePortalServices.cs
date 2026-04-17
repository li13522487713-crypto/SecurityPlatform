using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Coze.Abstractions;

/// <summary>
/// 工作空间首页内容 Provider。第一阶段实现为内存常量，
/// 第二阶段可换成读 PlatformContent 表（运营后台维护）。
/// </summary>
public interface IHomeContentService
{
    Task<HomeBannerDto> GetBannerAsync(TenantId tenantId, string workspaceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<HomeTutorialCardDto>> GetTutorialsAsync(
        TenantId tenantId,
        string workspaceId,
        CancellationToken cancellationToken);

    Task<PagedResult<HomeAnnouncementItemDto>> GetAnnouncementsAsync(
        TenantId tenantId,
        string workspaceId,
        HomeAnnouncementTab tab,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HomeRecommendedAgentDto>> GetRecommendedAgentsAsync(
        TenantId tenantId,
        string workspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HomeRecentActivityDto>> GetRecentActivitiesAsync(
        TenantId tenantId,
        string workspaceId,
        long currentUserId,
        CancellationToken cancellationToken);
}

public interface ICommunityService
{
    Task<PagedResult<CommunityWorkItemDto>> ListWorksAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);
}

public interface IPlatformGeneralService
{
    Task<IReadOnlyList<PlatformNoticeDto>> ListNoticesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<PlatformBrandingDto> GetBrandingAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public interface IMarketSummaryService
{
    Task<PagedResult<MarketCategorySummaryDto>> ListTemplateCategoriesAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<PagedResult<MarketCategorySummaryDto>> ListPluginCategoriesAsync(
        TenantId tenantId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);
}

public interface IMeSettingsService
{
    Task<MeGeneralSettingsDto> GetGeneralAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken);

    Task<MeGeneralSettingsDto> UpdateGeneralAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        MeGeneralSettingsUpdateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MePublishChannelDto>> ListPublishChannelsAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MeDataSourceDto>> ListDataSourcesAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken);

    Task DeleteAccountAsync(
        TenantId tenantId,
        CurrentUserInfo currentUser,
        CancellationToken cancellationToken);
}
