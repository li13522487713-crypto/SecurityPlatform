using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
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
/// 工作空间发布渠道主链在 AppHost 的真实承载。
/// 复用既有渠道与发布版本服务，供 app-web 的空间配置页与发布中心直连。
/// </summary>
[ApiController]
[Route("api/v1/workspaces/{workspaceId}/publish-channels")]
[Authorize]
public sealed class WorkspacePublishChannelsController : ControllerBase
{
    private readonly IWorkspacePublishChannelService _service;
    private readonly IWorkspaceChannelReleaseService _releaseService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public WorkspacePublishChannelsController(
        IWorkspacePublishChannelService service,
        IWorkspaceChannelReleaseService releaseService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _releaseService = releaseService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspacePublishChannelDto>>>> List(
        string workspaceId,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword
        };
        var result = await _service.ListAsync(tenantId, workspaceId, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkspacePublishChannelDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        string workspaceId,
        [FromBody] WorkspacePublishChannelCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var channelId = await _service.CreateAsync(tenantId, workspaceId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = channelId, ChannelId = channelId }, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{channelId}")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        string workspaceId,
        string channelId,
        [FromBody] WorkspacePublishChannelUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, workspaceId, channelId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{channelId}/reauth")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Reauthorize(
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.ReauthorizeAsync(tenantId, workspaceId, channelId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{channelId}")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, workspaceId, channelId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{channelId}/releases")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceChannelReleaseDto>>>> ListReleases(
        string workspaceId,
        string channelId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };
        var result = await _releaseService.ListAsync(tenantId, workspaceId, channelId, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkspaceChannelReleaseDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{channelId}/releases/{releaseId}")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<WorkspaceChannelReleaseDto>>> GetRelease(
        string workspaceId,
        string channelId,
        string releaseId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var release = await _releaseService.GetAsync(tenantId, workspaceId, channelId, releaseId, cancellationToken);
        return Ok(ApiResponse<WorkspaceChannelReleaseDto>.Ok(release, HttpContext.TraceIdentifier));
    }

    [HttpPost("{channelId}/releases")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<WorkspaceChannelReleaseDto>>> PublishRelease(
        string workspaceId,
        string channelId,
        [FromBody] WorkspaceChannelReleaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var release = await _releaseService.PublishAsync(tenantId, workspaceId, channelId, currentUser, request, cancellationToken);
        return Ok(ApiResponse<WorkspaceChannelReleaseDto>.Ok(release, HttpContext.TraceIdentifier));
    }

    [HttpPost("{channelId}/releases/rollback")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<WorkspaceChannelReleaseDto>>> RollbackRelease(
        string workspaceId,
        string channelId,
        [FromBody] WorkspaceChannelReleaseRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var release = await _releaseService.RollbackAsync(tenantId, workspaceId, channelId, currentUser, request, cancellationToken);
        return Ok(ApiResponse<WorkspaceChannelReleaseDto>.Ok(release, HttpContext.TraceIdentifier));
    }
}
