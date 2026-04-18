using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// DAG 工作流父级工程能力（M19 S19-1..S19-5），与现有 DagWorkflowController（/api/v2/workflows）并存：
///  - POST /api/v2/workflows/generate     AI 生成（auto / assisted）
///  - POST /api/v2/workflows/{id}/batch    批量执行（CSV / JSON / 数据库）
///  - POST /api/v2/workflows/{id}/compose  封装子工作流
///  - POST /api/v2/workflows/{id}/decompose 解散子工作流
///  - GET  /api/v2/workflows/quota         配额查询
///
/// /api/v2 中 v2 为 API 版本号（与产品「V2」无关），与 DagWorkflowController 同源。
/// </summary>
[ApiController]
[Route("api/v2/workflows")]
[Authorize]
public sealed class DagWorkflowEngineeringController : ControllerBase
{
    private readonly IWorkflowGenerationService _generation;
    private readonly IWorkflowBatchService _batch;
    private readonly IWorkflowCompositionService _composition;
    private readonly IWorkflowQuotaService _quota;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public DagWorkflowEngineeringController(
        IWorkflowGenerationService generation,
        IWorkflowBatchService batch,
        IWorkflowCompositionService composition,
        IWorkflowQuotaService quota,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUser)
    {
        _generation = generation;
        _batch = batch;
        _composition = composition;
        _quota = quota;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<WorkflowGenerationResult>>> Generate([FromBody] WorkflowGenerationRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _generation.GenerateAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<WorkflowGenerationResult>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/batch")]
    public async Task<ActionResult<ApiResponse<BatchExecuteResult>>> Batch(string id, [FromBody] BatchExecuteRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _batch.ExecuteBatchAsync(tenantId, user.UserId, request with { WorkflowId = id }, cancellationToken);
        return Ok(ApiResponse<BatchExecuteResult>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/compose")]
    public async Task<ActionResult<ApiResponse<WorkflowComposeResult>>> Compose(string id, [FromBody] WorkflowComposeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _composition.ComposeAsync(tenantId, user.UserId, request with { WorkflowId = id }, cancellationToken);
        return Ok(ApiResponse<WorkflowComposeResult>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/decompose")]
    public async Task<ActionResult<ApiResponse<object>>> Decompose(string id, [FromBody] WorkflowDecomposeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _composition.DecomposeAsync(tenantId, user.UserId, request with { WorkflowId = id }, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpGet("quota")]
    public async Task<ActionResult<ApiResponse<WorkflowQuotaDto>>> Quota(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var q = await _quota.GetQuotaAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<WorkflowQuotaDto>.Ok(q, HttpContext.TraceIdentifier));
    }
}
