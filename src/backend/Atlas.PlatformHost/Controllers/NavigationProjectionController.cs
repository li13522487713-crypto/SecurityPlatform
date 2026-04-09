using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/navigation")]
public sealed class NavigationProjectionController : ControllerBase
{
    private readonly INavigationProjectionService _projectionService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public NavigationProjectionController(
        INavigationProjectionService projectionService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _projectionService = projectionService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("platform")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<NavigationProjectionResponse>>> GetPlatform(
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<NavigationProjectionResponse>.Fail(
                ErrorCodes.Unauthorized,
                "Unauthorized",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _projectionService.GetPlatformProjectionAsync(
            tenantId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);
        return Ok(ApiResponse<NavigationProjectionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("apps/{appInstanceId:long}/workspace")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<NavigationProjectionResponse>>> GetWorkspace(
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<NavigationProjectionResponse>.Fail(
                ErrorCodes.Unauthorized,
                "Unauthorized",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _projectionService.GetWorkspaceProjectionAsync(
            tenantId,
            appInstanceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);
        return Ok(ApiResponse<NavigationProjectionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("apps/by-key/{appKey}/workspace")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<NavigationProjectionResponse>>> GetWorkspaceByAppKey(
        string appKey,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<NavigationProjectionResponse>.Fail(
                ErrorCodes.Unauthorized,
                "Unauthorized",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _projectionService.GetWorkspaceProjectionByAppKeyAsync(
            tenantId,
            appKey,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);
        return Ok(ApiResponse<NavigationProjectionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("runtime")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<NavigationProjectionResponse>>> GetRuntime(
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<NavigationProjectionResponse>.Fail(
                ErrorCodes.Unauthorized,
                "Unauthorized",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _projectionService.GetRuntimeProjectionAsync(
            tenantId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);
        return Ok(ApiResponse<NavigationProjectionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
