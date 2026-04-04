using Atlas.Application.Alert.Abstractions;
using Atlas.Application.Alert.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/alert")]
[PlatformOnly]
public sealed class AlertController : ControllerBase
{
    private readonly IAlertQueryService _alertQueryService;
    private readonly ITenantProvider _tenantProvider;

    public AlertController(IAlertQueryService alertQueryService, ITenantProvider tenantProvider)
    {
        _alertQueryService = alertQueryService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AlertView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AlertListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _alertQueryService.QueryAlertsAsync(request, tenantId, cancellationToken);
        var payload = ApiResponse<PagedResult<AlertListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}
