using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/application-catalogs")]
[Authorize]
public sealed class ApplicationCatalogsV2Controller : ControllerBase
{
    private readonly IApplicationCatalogQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;

    public ApplicationCatalogsV2Controller(
        IApplicationCatalogQueryService queryService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ApplicationCatalogListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<ApplicationCatalogListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<ApplicationCatalogDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ApplicationCatalogDetail>.Fail(ErrorCodes.NotFound, "Application catalog not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ApplicationCatalogDetail>.Ok(result, HttpContext.TraceIdentifier));
    }
}
