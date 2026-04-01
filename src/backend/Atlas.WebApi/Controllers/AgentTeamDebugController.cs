using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Application.AgentTeam.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/agent-teams")]
public sealed class AgentTeamDebugController : ControllerBase
{
    private readonly IAgentTeamCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<AgentTeamDebugRequest> _validator;

    public AgentTeamDebugController(
        IAgentTeamCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<AgentTeamDebugRequest> validator)
    {
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _validator = validator;
    }

    [HttpPost("{id:long}/debug")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AgentTeamDebugResult>>> Debug(
        long id,
        [FromBody] AgentTeamDebugRequest request,
        CancellationToken cancellationToken)
    {
        _validator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.DebugAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<AgentTeamDebugResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/sub-agents/{agentId:long}/debug")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AgentTeamDebugResult>>> DebugSubAgent(
        long id,
        long agentId,
        [FromBody] AgentTeamDebugRequest request,
        CancellationToken cancellationToken)
    {
        _validator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var narrowed = request with { FullChain = false, SubAgentId = agentId };
        var result = await _commandService.DebugAsync(tenantId, id, narrowed, cancellationToken);
        return Ok(ApiResponse<AgentTeamDebugResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
