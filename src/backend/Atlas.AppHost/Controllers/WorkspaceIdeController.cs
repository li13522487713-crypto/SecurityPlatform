using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/workspace-ide")]
[Authorize]
public sealed class WorkspaceIdeController : ControllerBase
{
    private readonly IWorkspaceIdeService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public WorkspaceIdeController(
        IWorkspaceIdeService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<WorkspaceIdeSummaryResponse>>> GetSummary(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _service.GetSummaryAsync(tenantId, userId, cancellationToken);
        return Ok(ApiResponse<WorkspaceIdeSummaryResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("dashboard-stats")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<WorkspaceIdeDashboardStatsResponse>>> GetDashboardStats(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _service.GetDashboardStatsAsync(tenantId, userId, cancellationToken);
        return Ok(ApiResponse<WorkspaceIdeDashboardStatsResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("resources")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceIdeResourceCardResponse>>>> GetResources(
        [FromQuery] string? keyword = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] bool favoriteOnly = false,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _service.GetResourcesAsync(
            tenantId,
            userId,
            new WorkspaceIdeResourceQueryRequest(keyword, resourceType, favoriteOnly, pageIndex, pageSize),
            cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkspaceIdeResourceCardResponse>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("apps")]
    [Authorize(Policy = PermissionPolicies.AiAppCreate)]
    public async Task<ActionResult<ApiResponse<WorkspaceIdeCreateAppResult>>> CreateApp(
        [FromBody] WorkspaceIdeCreateAppRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _service.CreateAppAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<WorkspaceIdeCreateAppResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("favorites/{resourceType}/{resourceId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateFavorite(
        string resourceType,
        long resourceId,
        [FromBody] WorkspaceIdeFavoriteUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _service.UpdateFavoriteAsync(tenantId, userId, resourceType, resourceId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { resourceType, resourceId }, HttpContext.TraceIdentifier));
    }

    [HttpPost("activities")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> RecordActivity(
        [FromBody] WorkspaceIdeActivityCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _service.RecordActivityAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { request.ResourceType, request.ResourceId }, HttpContext.TraceIdentifier));
    }

    [HttpGet("resources/{resourceType}/{resourceId}/references")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>>>> GetResourceReferences(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetResourceReferencesAsync(tenantId, resourceType, resourceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkspaceIdeResourceReferenceResponse>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("publish-center/items")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkspaceIdePublishCenterItemResponse>>>> GetPublishCenterItems(
        [FromQuery] string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPublishCenterItemsAsync(tenantId, resourceType, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WorkspaceIdePublishCenterItemResponse>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
