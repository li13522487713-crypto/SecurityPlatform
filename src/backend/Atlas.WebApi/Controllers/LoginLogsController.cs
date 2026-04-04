using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 登录日志查询（等保2.0：须记录所有登录事件，只读，按时间范围/状态/用户过滤）
/// </summary>
[ApiController]
[Route("api/v1/login-logs")]
[PlatformOnly]
public sealed class LoginLogsController : ControllerBase
{
    private readonly ILoginLogQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public LoginLogsController(ILoginLogQueryService queryService, ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LoginLogView)]
    public async Task<ActionResult<ApiResponse<PagedResult<LoginLogDto>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? username = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] bool? loginStatus = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetLoginLogsPagedAsync(
            tenantId, username, ipAddress, loginStatus, from, to, request.PageIndex, request.PageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<LoginLogDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("export")]
    [Authorize(Policy = PermissionPolicies.LoginLogView)]
    public async Task<IActionResult> Export(
        [FromQuery] string? username = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] bool? loginStatus = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ExportLoginLogsAsync(
            tenantId, username, ipAddress, loginStatus, from, to, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
