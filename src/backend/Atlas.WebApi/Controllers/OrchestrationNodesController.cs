using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Application.AgentTeam.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/agent-teams/{teamId:long}/nodes")]
[PlatformOnly]
public sealed class OrchestrationNodesController : ControllerBase
{
    private readonly IAgentTeamQueryService _queryService;
    private readonly IAgentTeamCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<OrchestrationNodeCreateRequest> _createValidator;
    private readonly IValidator<OrchestrationNodeUpdateRequest> _updateValidator;

    public OrchestrationNodesController(
        IAgentTeamQueryService queryService,
        IAgentTeamCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<OrchestrationNodeCreateRequest> createValidator,
        IValidator<OrchestrationNodeUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrchestrationNodeItem>>>> Get(long teamId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetNodesAsync(tenantId, teamId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrchestrationNodeItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(long teamId, [FromBody] OrchestrationNodeCreateRequest request, CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateNodeAsync(tenantId, teamId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{nodeId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long teamId, long nodeId, [FromBody] OrchestrationNodeUpdateRequest request, CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateNodeAsync(tenantId, teamId, nodeId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = nodeId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{nodeId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long teamId, long nodeId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteNodeAsync(tenantId, teamId, nodeId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = nodeId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("validate")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Validate(long teamId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.ValidateOrchestrationAsync(tenantId, teamId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TeamId = teamId.ToString(), Valid = true }, HttpContext.TraceIdentifier));
    }
}
