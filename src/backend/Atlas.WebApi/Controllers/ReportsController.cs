using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ReportsController(
        IReportService reportService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _reportService = reportService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReportDefinitionListItem>>>> Get(
        [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _reportService.QueryAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<ReportDefinitionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<ReportDefinitionDetail?>>> GetById(
        long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _reportService.GetByIdAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<ReportDefinitionDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] ReportDefinitionCreateRequest request, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _reportService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id, [FromBody] ReportDefinitionUpdateRequest request, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        var tenantId = _tenantProvider.GetTenantId();
        await _reportService.UpdateAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
        var tenantId = _tenantProvider.GetTenantId();
        await _reportService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
