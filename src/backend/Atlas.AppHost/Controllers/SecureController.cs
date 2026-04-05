using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/secure")]
public sealed class SecureController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;

    public SecureController(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    [HttpGet("ping")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public ActionResult<ApiResponse<object>> Ping()
    {
        var tenantId = _tenantProvider.GetTenantId();
        var data = new { Message = "PONG", TenantId = tenantId.ToString(), User = User.Identity?.Name };
        var payload = ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpGet("antiforgery")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public ActionResult<ApiResponse<object>> GetAntiforgeryToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var payload = ApiResponse<object>.Ok(new
        {
            Token = tokens.RequestToken,
            HeaderName = tokens.HeaderName
        }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}
