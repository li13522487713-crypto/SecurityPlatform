using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/team-agent-publications")]
[Authorize]
public sealed class TeamAgentPublicationsController : ControllerBase
{
    private readonly ITeamAgentPublicationService _publicationService;
    private readonly ITenantProvider _tenantProvider;

    public TeamAgentPublicationsController(
        ITeamAgentPublicationService publicationService,
        ITenantProvider tenantProvider)
    {
        _publicationService = publicationService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("team-agents/{teamAgentId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamAgentPublicationListItem>>>> GetByTeamAgent(
        long teamAgentId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _publicationService.GetByTeamAgentAsync(tenantId, teamAgentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeamAgentPublicationListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }
}
