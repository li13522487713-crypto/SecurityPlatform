using Atlas.Application.Alert.Abstractions;
using Atlas.Application.Alert.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("alert")]
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
    [Authorize]
    public ActionResult<ApiResponse<PagedResult<AlertListItem>>> Get([FromQuery] PagedRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = _alertQueryService.QueryAlerts(request, tenantId);
        var payload = ApiResponse<PagedResult<AlertListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}