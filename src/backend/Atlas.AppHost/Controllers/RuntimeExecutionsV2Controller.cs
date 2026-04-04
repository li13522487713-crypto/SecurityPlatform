using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v2/runtime-executions")]
[Authorize]
public sealed class RuntimeExecutionsV2Controller : ControllerBase
{
    private readonly IRuntimeExecutionQueryService _queryService;
    private readonly IRuntimeExecutionCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public RuntimeExecutionsV2Controller(
        IRuntimeExecutionQueryService queryService,
        IRuntimeExecutionCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RuntimeExecutionListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? appId,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? startedFrom,
        [FromQuery] DateTimeOffset? startedTo,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(
            tenantId,
            request,
            appId,
            status,
            startedFrom,
            startedTo,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<RuntimeExecutionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("stats")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionStats>>> GetStats(
        [FromQuery] string? appId,
        [FromQuery] DateTimeOffset? startedFrom,
        [FromQuery] DateTimeOffset? startedTo,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetStatsAsync(
            tenantId,
            appId,
            startedFrom,
            startedTo,
            cancellationToken);
        return Ok(ApiResponse<RuntimeExecutionStats>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<RuntimeExecutionDetail>.Fail(ErrorCodes.NotFound, "Runtime execution not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<RuntimeExecutionDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/audit-trails")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RuntimeExecutionAuditTrailItem>>>> GetAuditTrails(
        long id,
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAuditTrailsAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<RuntimeExecutionAuditTrailItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionOperationResult>>> Cancel(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<RuntimeExecutionOperationResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.CancelAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<RuntimeExecutionOperationResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/retry")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionOperationResult>>> Retry(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<RuntimeExecutionOperationResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.RetryAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<RuntimeExecutionOperationResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/resume")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionOperationResult>>> Resume(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<RuntimeExecutionOperationResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.ResumeAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<RuntimeExecutionOperationResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/debug")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionOperationResult>>> Debug(
        long id,
        [FromBody] RuntimeExecutionDebugRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<RuntimeExecutionOperationResult>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.DebugAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<RuntimeExecutionOperationResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/timeout-diagnosis")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<RuntimeExecutionTimeoutDiagnosis>>> GetTimeoutDiagnosis(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.GetTimeoutDiagnosisAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<RuntimeExecutionTimeoutDiagnosis>.Fail(ErrorCodes.NotFound, "Runtime execution not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<RuntimeExecutionTimeoutDiagnosis>.Ok(result, HttpContext.TraceIdentifier));
    }
}
