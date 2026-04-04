using Atlas.Application.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 在线用户（会话）管理（等保2.0：须支持管理员查看在线用户并强制下线）
/// </summary>
[ApiController]
[Route("api/v1/sessions")]
[PlatformOnly]
public sealed class SessionsController : ControllerBase
{
    private readonly ILoginLogQueryService _queryService;
    private readonly IAuthSessionRepository _authSessionRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly TimeProvider _timeProvider;

    public SessionsController(
        ILoginLogQueryService queryService,
        IAuthSessionRepository authSessionRepository,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        TimeProvider timeProvider)
    {
        _queryService = queryService;
        _authSessionRepository = authSessionRepository;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.OnlineUsersView)]
    public async Task<ActionResult<ApiResponse<PagedResult<OnlineUserDto>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetOnlineUsersPagedAsync(tenantId, keyword, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<OnlineUserDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 强制下线指定会话（等保2.0：操作须写入审计日志）
    /// </summary>
    [HttpDelete("{sessionId:long}")]
    [Authorize(Policy = PermissionPolicies.OnlineUsersForceLogout)]
    public async Task<ActionResult<ApiResponse<object>>> ForceLogout(
        long sessionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();

        var now = _timeProvider.GetUtcNow();
        var session = await _authSessionRepository.FindByIdAsync(tenantId, sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound(ApiResponse<object>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "ConversationNotFound"), HttpContext.TraceIdentifier));
        }

        await _authSessionRepository.RevokeAsync(tenantId, sessionId, now, cancellationToken);

        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var auditContext = new AuditContext(
            tenantId,
            actor,
            "FORCE_LOGOUT",
            "SUCCESS",
            sessionId.ToString(),
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { SessionId = sessionId.ToString() }, HttpContext.TraceIdentifier));
    }
}
