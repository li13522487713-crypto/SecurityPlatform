using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/agent-publications")]
[Authorize]
public sealed class AgentPublicationsController : ControllerBase
{
    private readonly IAgentPublicationService _publicationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AgentPublicationPublishRequest> _publishValidator;
    private readonly IValidator<AgentPublicationRollbackRequest> _rollbackValidator;

    public AgentPublicationsController(
        IAgentPublicationService publicationService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AgentPublicationPublishRequest> publishValidator,
        IValidator<AgentPublicationRollbackRequest> rollbackValidator)
    {
        _publicationService = publicationService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _publishValidator = publishValidator;
        _rollbackValidator = rollbackValidator;
    }

    [HttpGet("agents/{agentId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentPublicationListItem>>>> GetByAgent(
        long agentId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _publicationService.GetByAgentAsync(tenantId, agentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AgentPublicationListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("agents/{agentId:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentPublicationPublishResult>>> Publish(
        long agentId,
        [FromBody] AgentPublicationPublishRequest request,
        CancellationToken cancellationToken)
    {
        _publishValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var publisherUserId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _publicationService.PublishAsync(
            tenantId,
            agentId,
            publisherUserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<AgentPublicationPublishResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("agents/{agentId:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentPublicationPublishResult>>> Rollback(
        long agentId,
        [FromBody] AgentPublicationRollbackRequest request,
        CancellationToken cancellationToken)
    {
        _rollbackValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var operatorUserId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _publicationService.RollbackAsync(
            tenantId,
            agentId,
            operatorUserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<AgentPublicationPublishResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("agents/{agentId:long}/embed-token")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentEmbedTokenResult>>> RegenerateEmbedToken(
        long agentId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _publicationService.RegenerateEmbedTokenAsync(tenantId, agentId, cancellationToken);
        return Ok(ApiResponse<AgentEmbedTokenResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
