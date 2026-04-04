using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/dynamic-transform-jobs")]
public sealed class DynamicTransformJobsController : ControllerBase
{
    private readonly IDynamicTransformJobService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public DynamicTransformJobsController(
        IDynamicTransformJobService service,
        ITenantProvider tenantProvider,
        IAppContextAccessor appContextAccessor,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _appContextAccessor = appContextAccessor;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicTransformJobDto>>>> List(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ListAsync(tenantId, _appContextAccessor.ResolveAppId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicTransformJobDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTransformJobDto>>> Create([FromBody] DynamicTransformJobCreateRequest request, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicTransformJobDto>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.CreateAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<DynamicTransformJobDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{jobKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTransformJobDto?>>> GetByKey(string jobKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetAsync(tenantId, _appContextAccessor.ResolveAppId(), jobKey, cancellationToken);
        return Ok(ApiResponse<DynamicTransformJobDto?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{jobKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTransformJobDto>>> Update(
        string jobKey,
        [FromBody] DynamicTransformJobUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicTransformJobDto>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.UpdateAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), jobKey, request, cancellationToken);
        return Ok(ApiResponse<DynamicTransformJobDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{jobKey}/run")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTransformExecutionDto>>> Run(string jobKey, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicTransformExecutionDto>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.RunAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), jobKey, cancellationToken);
        return Ok(ApiResponse<DynamicTransformExecutionDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{jobKey}/pause")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTransformJobDto>>> Pause(string jobKey, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicTransformJobDto>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.PauseAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), jobKey, cancellationToken);
        return Ok(ApiResponse<DynamicTransformJobDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{jobKey}/resume")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTransformJobDto>>> Resume(string jobKey, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicTransformJobDto>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ResumeAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), jobKey, cancellationToken);
        return Ok(ApiResponse<DynamicTransformJobDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{jobKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string jobKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, _appContextAccessor.ResolveAppId(), jobKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { JobKey = jobKey }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{jobKey}/history")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicTransformExecutionDto>>>> History(
        string jobKey,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetHistoryAsync(tenantId, _appContextAccessor.ResolveAppId(), request, jobKey, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicTransformExecutionDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{jobKey}/executions/{executionId}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTransformExecutionDto?>>> GetExecution(
        string jobKey,
        string executionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetExecutionAsync(tenantId, _appContextAccessor.ResolveAppId(), jobKey, executionId, cancellationToken);
        return Ok(ApiResponse<DynamicTransformExecutionDto?>.Ok(result, HttpContext.TraceIdentifier));
    }
}
