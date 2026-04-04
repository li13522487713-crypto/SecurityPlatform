using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/evaluations/comparisons")]
[Authorize]
[PlatformOnly]
public sealed class EvaluationComparisonController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;
    private readonly ITenantProvider _tenantProvider;

    public EvaluationComparisonController(
        IEvaluationService evaluationService,
        ITenantProvider tenantProvider)
    {
        _evaluationService = evaluationService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<EvaluationComparisonResult>>> Compare(
        [FromQuery] long leftTaskId,
        [FromQuery] long rightTaskId,
        CancellationToken cancellationToken)
    {
        if (leftTaskId <= 0 || rightTaskId <= 0)
        {
            return BadRequest(ApiResponse<EvaluationComparisonResult>.Fail(
                ErrorCodes.ValidationError,
                "leftTaskId/rightTaskId 必须大于 0。",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _evaluationService.CompareTasksAsync(
            tenantId,
            leftTaskId,
            rightTaskId,
            cancellationToken);
        return Ok(ApiResponse<EvaluationComparisonResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
