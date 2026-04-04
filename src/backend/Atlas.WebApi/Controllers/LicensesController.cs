using Atlas.Application.Governance.Abstractions;
using Atlas.Application.Governance.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/licenses")]
[Authorize]
[PlatformOnly]
public sealed class LicensesController : ControllerBase
{
    private readonly ILicenseGrantService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public LicensesController(
        ILicenseGrantService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("offline-request")]
    public async Task<ActionResult<ApiResponse<object>>> CreateOfflineRequest(
        [FromBody] LicenseOfflineRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var token = await _service.CreateOfflineRequestAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { RequestToken = token }, HttpContext.TraceIdentifier));
    }

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<LicenseValidateResponse>>> Import(
        [FromBody] LicenseImportRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<LicenseValidateResponse>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ImportAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<LicenseValidateResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("validate")]
    public async Task<ActionResult<ApiResponse<LicenseValidateResponse>>> Validate(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ValidateAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<LicenseValidateResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("renew")]
    public async Task<ActionResult<ApiResponse<LicenseValidateResponse>>> Renew(
        [FromBody] LicenseImportRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<LicenseValidateResponse>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ImportAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<LicenseValidateResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("audit")]
    public ActionResult<ApiResponse<object>> Audit()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            Items = Array.Empty<object>()
        }, HttpContext.TraceIdentifier));
    }
}
