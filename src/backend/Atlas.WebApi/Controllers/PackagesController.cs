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
[Route("api/v1/packages")]
[Authorize]
[PlatformOnly]
public sealed class PackagesController : ControllerBase
{
    private readonly IPackageService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PackagesController(IPackageService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("export")]
    public async Task<ActionResult<ApiResponse<PackageOperationResponse>>> Export([FromBody] PackageExportRequest request, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<PackageOperationResponse>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ExportAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<PackageOperationResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<PackageOperationResponse>>> Import([FromBody] PackageImportRequest request, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<PackageOperationResponse>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ImportAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<PackageOperationResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<ApiResponse<PackageOperationResponse>>> Analyze([FromBody] PackageAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<PackageOperationResponse>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.AnalyzeAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<PackageOperationResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
