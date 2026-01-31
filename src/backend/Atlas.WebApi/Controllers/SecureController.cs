using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/secure")]
public sealed class SecureController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;

    public SecureController(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    [HttpGet("ping")]
    [Authorize]
    public ActionResult<ApiResponse<object>> Ping()
    {
        var tenantId = _tenantProvider.GetTenantId();
        var data = new { Message = "PONG", TenantId = tenantId.ToString(), User = User.Identity?.Name };
        var payload = ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}
