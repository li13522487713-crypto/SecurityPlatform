using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/apps/{appId:long}/schema-change-tasks")]
public sealed class SchemaChangeTasksController : ControllerBase
{
    private readonly ISchemaChangeTaskService _taskService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;

    public SchemaChangeTasksController(
        ISchemaChangeTaskService taskService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder)
    {
        _taskService = taskService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SchemaChangeTaskListItem>>>> List(
        long appId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var tasks = await _taskService.ListByAppAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SchemaChangeTaskListItem>>.Ok(tasks, HttpContext.TraceIdentifier));
    }

    [HttpGet("{taskId:long}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaChangeTaskListItem?>>> GetById(
        long appId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var task = await _taskService.GetByIdAsync(tenantId, taskId, cancellationToken);
        return Ok(ApiResponse<SchemaChangeTaskListItem?>.Ok(task, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        long appId,
        [FromBody] SchemaChangeTaskCreateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _taskService.CreateAndExecuteAsync(tenantId, currentUser.UserId, request, cancellationToken);
        await RecordAuditAsync(currentUser, "CREATE_SCHEMA_CHANGE_TASK", appId.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{taskId:long}/cancel")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(
        long appId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _taskService.CancelAsync(tenantId, taskId, cancellationToken);
        await RecordAuditAsync(currentUser, "CANCEL_SCHEMA_CHANGE_TASK", taskId.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TaskId = taskId.ToString() }, HttpContext.TraceIdentifier));
    }

    private Task RecordAuditAsync(
        CurrentUserInfo currentUser,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var context = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        return _auditRecorder.RecordAsync(context, cancellationToken);
    }
}
