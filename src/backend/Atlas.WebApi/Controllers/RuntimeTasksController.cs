using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/runtime")]
[Authorize]
public sealed class RuntimeTasksController : ControllerBase
{
    private readonly IRuntimeRouteQueryService _runtimeService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public RuntimeTasksController(
        IRuntimeRouteQueryService runtimeService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _runtimeService = runtimeService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("apps/{appKey}/pages/{pageKey}")]
    public async Task<ActionResult<ApiResponse<RuntimePageResponse?>>> GetRuntimePage(
        string appKey,
        string pageKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _runtimeService.GetRuntimePageAsync(tenantId, appKey, pageKey, cancellationToken);
        return Ok(ApiResponse<RuntimePageResponse?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<ApiResponse<PagedResult<RuntimeTaskListItem>>>> GetTasks(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<PagedResult<RuntimeTaskListItem>>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _runtimeService.GetRuntimeTasksAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<RuntimeTaskListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("apps/{appKey}/menu")]
    public async Task<ActionResult<ApiResponse<RuntimeMenuResponse>>> GetRuntimeMenu(
        string appKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _runtimeService.GetRuntimeMenuAsync(tenantId, appKey, cancellationToken);
        return Ok(ApiResponse<RuntimeMenuResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("tasks/{taskId:long}/actions")]
    public async Task<ActionResult<ApiResponse<object>>> ExecuteTaskAction(
        long taskId,
        [FromBody] RuntimeTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var ok = await _runtimeService.ExecuteRuntimeTaskActionAsync(tenantId, currentUser.UserId, taskId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = ok }, HttpContext.TraceIdentifier));
    }
}
