using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Coze PRD 工作空间-发布渠道（PRD 04-4.6）。
/// </summary>
[ApiController]
[Route("api/v1/workspaces/{workspaceId}/publish-channels")]
[Authorize]
public sealed class WorkspacePublishChannelsController : ControllerBase
{
    private readonly IWorkspacePublishChannelService _service;
    private readonly ITenantProvider _tenantProvider;

    public WorkspacePublishChannelsController(
        IWorkspacePublishChannelService service,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
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
        var paged = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword };
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
}
