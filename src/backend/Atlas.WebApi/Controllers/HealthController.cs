using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ApiResponse<string>> Get()
    {
        var payload = ApiResponse<string>.Ok("OK", HttpContext.TraceIdentifier);
        return Ok(payload);
    }
}
