using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Atlas.AppHost.Controllers;

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
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationQueryService queryService,
        INotificationCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        ILogger<NotificationsController> logger)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _logger = logger;
    }

    [HttpGet]
    [HttpGet("inbox")]
    public async Task<ActionResult<ApiResponse<PagedResult<UserNotificationDto>>>> GetMyNotifications(
        [FromQuery] PagedRequest request,
        [FromQuery] bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();
        _logger.LogInformation("[Notifications/Inbox] 开始查询 PageIndex={PageIndex} PageSize={PageSize} isRead={IsRead} UserId={UserId}",
            request.PageIndex, request.PageSize, isRead, currentUser.UserId);

        var query = new UserNotificationPagedQuery(request.PageIndex, request.PageSize, isRead);
        var result = await _queryService.GetUserNotificationsAsync(tenantId, currentUser.UserId, query, cancellationToken);
        _logger.LogInformation("[Notifications/Inbox] 查询完成 共{Total}条 返回{Count}条 耗时{Elapsed}ms",
            result.Total, result.Items.Count, sw.ElapsedMilliseconds);

        return Ok(ApiResponse<PagedResult<UserNotificationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        var count = await _queryService.GetUnreadCountAsync(tenantId, currentUser.UserId, cancellationToken);
        _logger.LogInformation("[Notifications/UnreadCount] 未读数={Count} 耗时{Elapsed}ms", count, sw.ElapsedMilliseconds);
        return Ok(ApiResponse<object>.Ok(new { count }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{notificationId:long}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(
        long notificationId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        await _commandService.MarkReadAsync(tenantId, currentUser.UserId, notificationId, cancellationToken);
        await RecordAuditAsync("NOTIFICATION_READ", notificationId.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, HttpContext.TraceIdentifier));
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllRead(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        var unreadBefore = await _queryService.GetUnreadCountAsync(tenantId, currentUser.UserId, cancellationToken);
        await _commandService.MarkAllReadAsync(tenantId, currentUser.UserId, cancellationToken);
        if (unreadBefore > 0)
        {
            await RecordAuditAsync("NOTIFICATION_READ", $"all:{unreadBefore}", cancellationToken);
        }
        return Ok(ApiResponse<object>.Ok(new { }, HttpContext.TraceIdentifier));
    }

    [HttpGet("manage")]
    [Authorize(Policy = PermissionPolicies.NotificationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetManage(
        [FromQuery] PagedRequest request,
        [FromQuery] string? title = null,
        [FromQuery] string? noticeType = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var query = new NotificationPagedQuery(request.PageIndex, request.PageSize, title, noticeType, isActive);
        var result = await _queryService.GetPagedAsync(tenantId, query, cancellationToken);
        return Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

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

        await RecordAuditAsync("NOTIFICATION_PUBLISH", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("manage/{id:long}")]
    [Authorize(Policy = PermissionPolicies.NotificationUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] NotificationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);

        await RecordAuditAsync("NOTIFICATION_UPDATE", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("manage/{id:long}")]
    [Authorize(Policy = PermissionPolicies.NotificationDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);

        await RecordAuditAsync("NOTIFICATION_DELETE", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("manage/{id:long}/revoke")]
    [Authorize(Policy = PermissionPolicies.NotificationUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.RevokeAsync(tenantId, id, cancellationToken);

        await RecordAuditAsync("NOTIFICATION_UPDATE", $"revoke:{id}", cancellationToken);
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
}
