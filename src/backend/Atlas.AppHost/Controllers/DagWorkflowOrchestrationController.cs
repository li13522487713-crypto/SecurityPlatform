using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 双哲学编排 / 节点状态 API（M20 S20-6 / S20-7）。
/// </summary>
[ApiController]
[Route("api/v2/workflows/orchestration")]
[Authorize]
public sealed class DagWorkflowOrchestrationController : ControllerBase
{
    private readonly IDualOrchestrationEngine _engine;

    public DagWorkflowOrchestrationController(IDualOrchestrationEngine engine)
    {
        _engine = engine;
    }

    public sealed record PlanRequest(string CanvasJson, string Mode, IReadOnlyList<OrchestrationTool>? Tools);

    [HttpPost("plan")]
    public ActionResult<ApiResponse<OrchestrationPlan>> Plan([FromBody] PlanRequest request)
    {
        var p = _engine.Plan(request.CanvasJson, request.Mode, request.Tools);
        return Ok(ApiResponse<OrchestrationPlan>.Ok(p, HttpContext.TraceIdentifier));
    }
}
