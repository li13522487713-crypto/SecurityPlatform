using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// Coze PRD 工作空间首页内容（PRD 01）。所有数据按工作空间维度返回，
/// 路径：<c>/api/v1/workspaces/{workspaceId}/home/{slot}</c>。
/// </summary>
[ApiController]
[Route("api/v1/workspaces/{workspaceId}/home")]
[Authorize]
public sealed class HomeContentController : ControllerBase
{
    private readonly IHomeContentService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public HomeContentController(
        IHomeContentService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("banner")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<HomeBannerDto>>> GetBanner(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetBannerAsync(tenantId, workspaceId, cancellationToken);
        return Ok(ApiResponse<HomeBannerDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("tutorials")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HomeTutorialCardDto>>>> GetTutorials(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetTutorialsAsync(tenantId, workspaceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HomeTutorialCardDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("announcements")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<HomeAnnouncementItemDto>>>> GetAnnouncements(
        string workspaceId,
        [FromQuery] string? tab = "all",
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var normalizedTab = string.Equals(tab, "notice", StringComparison.OrdinalIgnoreCase)
            ? HomeAnnouncementTab.Notice
            : HomeAnnouncementTab.All;

        var paged = new PagedRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword
        };

        var result = await _service.GetAnnouncementsAsync(tenantId, workspaceId, normalizedTab, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<HomeAnnouncementItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("recommended-agents")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HomeRecommendedAgentDto>>>> GetRecommendedAgents(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetRecommendedAgentsAsync(tenantId, workspaceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HomeRecommendedAgentDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("recent-activities")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HomeRecentActivityDto>>>> GetRecentActivities(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetRecentActivitiesAsync(tenantId, workspaceId, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HomeRecentActivityDto>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
