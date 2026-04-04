using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Application.AgentTeam.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/agent-team-runs")]
public sealed class AgentTeamRunsController : ControllerBase
{
    private readonly IAgentTeamQueryService _queryService;
    private readonly IAgentTeamCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AgentTeamRunCreateRequest> _runCreateValidator;
    private readonly IValidator<AgentTeamRunInterveneRequest> _interveneValidator;

    public AgentTeamRunsController(
        IAgentTeamQueryService queryService,
        IAgentTeamCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AgentTeamRunCreateRequest> runCreateValidator,
        IValidator<AgentTeamRunInterveneRequest> interveneValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _runCreateValidator = runCreateValidator;
        _interveneValidator = interveneValidator;
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] AgentTeamRunCreateRequest request, CancellationToken cancellationToken)
    {
        _runCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var triggerBy = _currentUserAccessor.GetCurrentUserOrThrow().UserId.ToString();
        var runId = await _commandService.CreateRunAsync(tenantId, triggerBy, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = runId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{runId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AgentTeamRunDetail>>> Get(long runId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var run = await _queryService.GetRunAsync(tenantId, runId, cancellationToken);
        if (run is null)
        {
            return NotFound(ApiResponse<AgentTeamRunDetail>.Fail(ErrorCodes.NotFound, "未找到执行实例。", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AgentTeamRunDetail>.Ok(run, HttpContext.TraceIdentifier));
    }

    [HttpGet("{runId:long}/nodes")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NodeRunItem>>>> GetNodes(long runId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var nodes = await _queryService.GetRunNodesAsync(tenantId, runId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NodeRunItem>>.Ok(nodes, HttpContext.TraceIdentifier));
    }

    [HttpGet("{runId:long}/interventions")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NodeRunItem>>>> GetInterventions(long runId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var nodes = await _queryService.GetRunNodesAsync(tenantId, runId, cancellationToken);
        var waiting = nodes.Where(x => x.HumanInterventionAllowed && x.State == Domain.AgentTeam.Entities.NodeRunStatus.WaitingApproval).ToList();
        return Ok(ApiResponse<IReadOnlyList<NodeRunItem>>.Ok(waiting, HttpContext.TraceIdentifier));
    }

    [HttpPost("{runId:long}/cancel")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long runId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.CancelRunAsync(tenantId, runId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = runId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{runId:long}/nodes/{nodeRunId:long}/intervene")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> Intervene(
        long runId,
        long nodeRunId,
        [FromBody] AgentTeamRunInterveneRequest request,
        CancellationToken cancellationToken)
    {
        _interveneValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.InterveneRunAsync(tenantId, runId, nodeRunId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = nodeRunId.ToString() }, HttpContext.TraceIdentifier));
    }
}
