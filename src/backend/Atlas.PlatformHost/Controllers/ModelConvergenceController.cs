using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/model-convergence")]
[Authorize]
public sealed class ModelConvergenceController : ControllerBase
{
    private readonly IModelConvergenceService _service;
    private readonly ITenantProvider _tenantProvider;

    public ModelConvergenceController(
        IModelConvergenceService service,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("analysis")]
    [Authorize(Policy = PermissionPolicies.ModelConfigView)]
    public async Task<ActionResult<ApiResponse<ModelConvergenceResponse>>> Analyze(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.AnalyzeAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<ModelConvergenceResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
