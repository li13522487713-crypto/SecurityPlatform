using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/resource-center")]
[Authorize]
public sealed class ResourceCenterV2Controller : ControllerBase
{
    private readonly IResourceCenterQueryService _resourceCenterQueryService;
    private readonly IResourceCenterCommandService _resourceCenterCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ResourceCenterV2Controller(
        IResourceCenterQueryService resourceCenterQueryService,
        IResourceCenterCommandService resourceCenterCommandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _resourceCenterQueryService = resourceCenterQueryService;
        _resourceCenterCommandService = resourceCenterCommandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("groups")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ResourceCenterGroupsResponse>>> GetGroups(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var response = await _resourceCenterQueryService.GetGroupsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<ResourceCenterGroupsResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpGet("groups/summary")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ResourceCenterGroupsSummaryResponse>>> GetGroupsSummary(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var response = await _resourceCenterQueryService.GetGroupsSummaryAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<ResourceCenterGroupsSummaryResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpGet("datasource-consumption")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ResourceCenterDataSourceConsumptionResponse>>> GetDataSourceConsumption(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var response = await _resourceCenterQueryService.GetDataSourceConsumptionAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<ResourceCenterDataSourceConsumptionResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpGet("datasource-consumption/summary")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ResourceCenterDataSourceConsumptionSummaryResponse>>> GetDataSourceConsumptionSummary(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var response = await _resourceCenterQueryService.GetDataSourceConsumptionSummaryAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<ResourceCenterDataSourceConsumptionSummaryResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpPost("datasource-consumption/repair/disable-invalid-binding")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<ResourceCenterRepairResult>>> DisableInvalidBinding(
        [FromBody] DisableInvalidBindingRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<ResourceCenterRepairResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _resourceCenterCommandService.DisableInvalidBindingAsync(
            tenantId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<ResourceCenterRepairResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("datasource-consumption/repair/switch-primary-binding")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<ResourceCenterRepairResult>>> SwitchPrimaryBinding(
        [FromBody] SwitchPrimaryBindingRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<ResourceCenterRepairResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _resourceCenterCommandService.SwitchPrimaryBindingAsync(
            tenantId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<ResourceCenterRepairResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("datasource-consumption/repair/unbind-orphan-binding")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<ResourceCenterRepairResult>>> UnbindOrphanBinding(
        [FromBody] UnbindOrphanBindingRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<ResourceCenterRepairResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _resourceCenterCommandService.UnbindOrphanBindingAsync(
            tenantId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<ResourceCenterRepairResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
