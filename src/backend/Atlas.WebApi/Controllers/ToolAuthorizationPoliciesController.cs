using Atlas.Application.Governance.Abstractions;
using Atlas.Application.Governance.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/tools")]
[Authorize]
[PlatformOnly]
public sealed class ToolAuthorizationPoliciesController : ControllerBase
{
    private readonly IToolAuthorizationService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ToolAuthorizationPoliciesController(
        IToolAuthorizationService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("authorization-policies")]
    public async Task<ActionResult<ApiResponse<PagedResult<ToolAuthorizationPolicyResponse>>>> Query(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.QueryPoliciesAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<ToolAuthorizationPolicyResponse>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("authorization-policies")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] ToolAuthorizationPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.CreatePolicyAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("authorization-policies/{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] ToolAuthorizationPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdatePolicyAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("simulate")]
    public async Task<ActionResult<ApiResponse<ToolAuthorizationSimulateResponse>>> Simulate(
        [FromBody] ToolAuthorizationSimulateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.SimulateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<ToolAuthorizationSimulateResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("audit")]
    public ActionResult<ApiResponse<object>> Audit()
    {
        return Ok(ApiResponse<object>.Ok(new { Message = ApiResponseLocalizer.T(HttpContext, "ToolAuditQueryPlaceholder") }, HttpContext.TraceIdentifier));
    }
}
