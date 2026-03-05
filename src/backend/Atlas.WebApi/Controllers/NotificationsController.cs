using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 通知公告管理（等保2.0：公告操作须有审计）
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationQueryService _queryService;
    private readonly INotificationCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;

    public NotificationsController(
        INotificationQueryService queryService,
        INotificationCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
    }

    // ===== 用户端接口 =====

    /// <summary>当前用户通知列表（分页）</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserNotificationDto>>>> GetMyNotifications(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();
        #region agent log
        AgentDebugLog(
            "H4",
            "NotificationsController.cs:GetMyNotifications:entry",
            "get my notifications entry",
            new { userId = currentUser.UserId, tenantId = tenantId.ToString(), pageIndex, pageSize, isRead });
        #endregion

        var query = new UserNotificationPagedQuery(pageIndex, pageSize, isRead);
        var result = await _queryService.GetUserNotificationsAsync(tenantId, currentUser.UserId, query, cancellationToken);
        return Ok(ApiResponse<PagedResult<UserNotificationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>未读通知数量</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();
        #region agent log
        AgentDebugLog(
            "H4",
            "NotificationsController.cs:GetUnreadCount:entry",
            "get unread count entry",
            new { userId = currentUser.UserId, tenantId = tenantId.ToString() });
        #endregion

        var count = await _queryService.GetUnreadCountAsync(tenantId, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { count }, HttpContext.TraceIdentifier));
    }

    /// <summary>标记单条已读</summary>
    [HttpPut("{notificationId:long}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(
        long notificationId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        await _commandService.MarkReadAsync(tenantId, currentUser.UserId, notificationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, HttpContext.TraceIdentifier));
    }

    /// <summary>全部标记已读</summary>
    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllRead(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        await _commandService.MarkAllReadAsync(tenantId, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, HttpContext.TraceIdentifier));
    }

    // ===== 管理员端接口 =====

    /// <summary>管理员：分页查询公告列表</summary>
    [HttpGet("manage")]
    [Authorize(Policy = PermissionPolicies.NotificationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetManage(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? title = null,
        [FromQuery] string? noticeType = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var query = new NotificationPagedQuery(pageIndex, pageSize, title, noticeType, isActive);
        var result = await _queryService.GetPagedAsync(tenantId, query, cancellationToken);
        return Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>管理员：创建公告</summary>
    [HttpPost("manage")]
    [Authorize(Policy = PermissionPolicies.NotificationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] NotificationCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        var id = await _commandService.CreateAsync(
            tenantId, currentUser.UserId,
            currentUser.Username ?? currentUser.UserId.ToString(),
            request, cancellationToken);

        await RecordAuditAsync("CREATE_NOTIFICATION", request.Title, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>管理员：更新公告</summary>
    [HttpPut("manage/{id:long}")]
    [Authorize(Policy = PermissionPolicies.NotificationUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] NotificationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);

        await RecordAuditAsync("UPDATE_NOTIFICATION", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>管理员：删除公告（等保2.0：记录审计）</summary>
    [HttpDelete("manage/{id:long}")]
    [Authorize(Policy = PermissionPolicies.NotificationDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);

        await RecordAuditAsync("DELETE_NOTIFICATION", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    private async Task RecordAuditAsync(string action, string target, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return;

        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;

        var auditContext = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);
    }

    private static void AgentDebugLog(string hypothesisId, string location, string message, object data)
    {
        try
        {
            var payload = new
            {
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            global::System.IO.File.AppendAllText("/opt/cursor/logs/debug.log", JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // ignore debug log failures
        }
    }
}
