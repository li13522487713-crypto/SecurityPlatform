using Atlas.Application.AgentTeam.Abstractions;
using Atlas.Application.AgentTeam.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/agent-teams/{teamId:long}/sub-agents")]
public sealed class SubAgentsController : ControllerBase
{
    private readonly IAgentTeamQueryService _queryService;
    private readonly IAgentTeamCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<SubAgentCreateRequest> _createValidator;
    private readonly IValidator<SubAgentUpdateRequest> _updateValidator;

    public SubAgentsController(
        IAgentTeamQueryService queryService,
        IAgentTeamCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<SubAgentCreateRequest> createValidator,
        IValidator<SubAgentUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubAgentItem>>>> Get(long teamId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetSubAgentsAsync(tenantId, teamId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SubAgentItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(long teamId, [FromBody] SubAgentCreateRequest request, CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateSubAgentAsync(tenantId, teamId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{subAgentId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long teamId,
        long subAgentId,
        [FromBody] SubAgentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateSubAgentAsync(tenantId, teamId, subAgentId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = subAgentId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{subAgentId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long teamId, long subAgentId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteSubAgentAsync(tenantId, teamId, subAgentId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = subAgentId.ToString() }, HttpContext.TraceIdentifier));
    }
}
