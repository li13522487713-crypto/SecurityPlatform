using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 应用草稿锁控制器（设计态前缀 /api/v1/lowcode/apps/{id}/draft-lock）。
/// AppHost 侧镜像，供 app-web 在 direct 模式下调用。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/apps/{appId:long}/draft-lock")]
public sealed class LowCodeAppDraftLockController : ControllerBase
{
    private readonly IAppDraftLockService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAppDraftLockController(IAppDraftLockService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("acquire")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<AppDraftLockResult>>> Acquire(long appId, [FromBody] AcquireRequest req, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var result = await _service.TryAcquireAsync(tenantId, appId, user.UserId, req.SessionId, cancellationToken);
        return Ok(ApiResponse<AppDraftLockResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("renew")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Renew(long appId, [FromBody] RenewRequest req, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.RenewAsync(tenantId, appId, req.SessionId, cancellationToken);
        if (!result.Acquired)
        {
            //throw new BusinessException(ErrorCodes.Conflict, "锁已被他人夺取或不存在，请重新获取");
        }

        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("release")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Release(long appId, [FromBody] ReleaseRequest req, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.ReleaseAsync(tenantId, appId, req.SessionId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("takeover")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<AppDraftLockResult>>> Takeover(long appId, [FromBody] TakeoverRequest req, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var result = await _service.ForceTakeoverAsync(tenantId, appId, user.UserId, req.SessionId, cancellationToken);
        return Ok(ApiResponse<AppDraftLockResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("status")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppDraftLockInfo?>>> Status(long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var info = await _service.GetCurrentAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<AppDraftLockInfo?>.Ok(info, HttpContext.TraceIdentifier));
    }

    public sealed record AcquireRequest(string SessionId);
    public sealed record RenewRequest(string SessionId);
    public sealed record ReleaseRequest(string SessionId);
    public sealed record TakeoverRequest(string SessionId);
}
