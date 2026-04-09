using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/agentic-rag")]
[Authorize]
public sealed class AgenticRagController : ControllerBase
{
    private readonly IAgenticRagOrchestrationService _service;
    private readonly ITenantProvider _tenantProvider;

    public AgenticRagController(
        IAgenticRagOrchestrationService service,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("query")]
    [Authorize(Policy = PermissionPolicies.AiSearchView)]
    public async Task<ActionResult<ApiResponse<AgenticRagQueryResponse>>> Query(
        [FromBody] AgenticRagQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.QueryAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<AgenticRagQueryResponse>.Ok(result, HttpContext.TraceIdentifier));
    }
}
