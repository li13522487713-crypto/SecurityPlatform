using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 运行时版本管理（M14 S14-1，**runtime 前缀** /api/runtime/versions）。
///
/// - POST /archive             把当前生效版本归档（系统快照）
/// - POST /{versionId}:rollback 一键回滚到指定版本（影响所有运行实例）
/// </summary>
[ApiController]
[Route("api/runtime/versions")]
[Authorize]
public sealed class RuntimeVersionsController : ControllerBase
{
    private readonly IAppVersioningService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeVersionsController(IAppVersioningService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    public sealed record ArchiveRequest(string AppId);
    public sealed record RollbackRequest(string AppId);

    [HttpPost("archive")]
    public async Task<ActionResult<ApiResponse<object>>> Archive([FromBody] ArchiveRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var appId = long.Parse(request.AppId);
        var versionId = await _service.ArchiveCurrentAsync(tenantId, user.UserId, appId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { versionId = versionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{versionId:long}:rollback")]
    public async Task<ActionResult<ApiResponse<object>>> Rollback(long versionId, [FromBody] RollbackRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var appId = long.Parse(request.AppId);
        await _service.RollbackAsync(tenantId, user.UserId, appId, versionId, new AppVersionRollbackRequest(Note: "runtime rollback"), cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
