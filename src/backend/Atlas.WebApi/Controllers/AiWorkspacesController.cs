using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/ai-workspaces")]
[Authorize]
[PlatformOnly]
public sealed class AiWorkspacesController : ControllerBase
{
    private readonly IAiWorkspaceService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AiWorkspaceUpdateRequest> _updateValidator;

    public AiWorkspacesController(
        IAiWorkspaceService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AiWorkspaceUpdateRequest> updateValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _updateValidator = updateValidator;
    }

    [HttpGet("current")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<AiWorkspaceDto>>> GetCurrent(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.GetCurrentAsync(tenantId, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<AiWorkspaceDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("current")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<AiWorkspaceDto>>> UpdateCurrent(
        [FromBody] AiWorkspaceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var result = await _service.UpdateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<AiWorkspaceDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("library")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<AiLibraryPagedResult>>> GetLibrary(
        [FromQuery] string? keyword = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetLibraryAsync(
            tenantId,
            new AiLibraryQueryRequest(keyword, resourceType, pageIndex, pageSize),
            cancellationToken);
        return Ok(ApiResponse<AiLibraryPagedResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
