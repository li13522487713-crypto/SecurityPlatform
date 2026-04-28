using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/workspaces/{workspaceId}")]
[Authorize]
public sealed class WorkspaceEvaluationsController : ControllerBase
{
    private readonly IWorkspaceEvaluationService _evaluations;
    private readonly IWorkspaceTestsetService _testsets;
    private readonly ITenantProvider _tenantProvider;

    public WorkspaceEvaluationsController(
        IWorkspaceEvaluationService evaluations,
        IWorkspaceTestsetService testsets,
        ITenantProvider tenantProvider)
    {
        _evaluations = evaluations;
        _testsets = testsets;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("evaluations")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<EvaluationItemDto>>>> ListEvaluations(
        string workspaceId,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword };
        var result = await _evaluations.ListAsync(tenantId, workspaceId, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<EvaluationItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("evaluations/{evaluationId}")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<EvaluationDetailDto>>> GetEvaluation(
        string workspaceId,
        string evaluationId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _evaluations.GetAsync(tenantId, workspaceId, evaluationId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<EvaluationDetailDto>.Fail(
                ErrorCodes.NotFound,
                "Evaluation not found.",
                HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<EvaluationDetailDto>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("testsets")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TestsetItemDto>>>> ListTestsets(
        string workspaceId,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword };
        var result = await _testsets.ListAsync(tenantId, workspaceId, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<TestsetItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("testsets")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateTestset(
        string workspaceId,
        [FromBody] TestsetCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _testsets.CreateAsync(tenantId, workspaceId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id, TestsetId = id }, HttpContext.TraceIdentifier));
    }
}
