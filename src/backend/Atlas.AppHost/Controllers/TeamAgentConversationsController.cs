using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/team-agent-conversations")]
public sealed class TeamAgentConversationsController : ControllerBase
{
    private readonly ITeamAgentService _teamAgentService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<TeamAgentConversationUpdateRequest> _updateValidator;

    public TeamAgentConversationsController(
        ITeamAgentService teamAgentService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<TeamAgentConversationUpdateRequest> updateValidator)
    {
        _teamAgentService = teamAgentService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _updateValidator = updateValidator;
    }

    [HttpGet("{conversationId:long}")]
    [Authorize(Policy = PermissionPolicies.ConversationView)]
    public async Task<ActionResult<ApiResponse<TeamAgentConversationDto?>>> Get(long conversationId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _teamAgentService.GetConversationAsync(tenantId, userId, conversationId, cancellationToken);
        return Ok(ApiResponse<TeamAgentConversationDto?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{conversationId:long}/messages")]
    [Authorize(Policy = PermissionPolicies.ConversationView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamAgentMessageDto>>>> GetMessages(
        long conversationId,
        [FromQuery] bool includeContextMarkers = false,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _teamAgentService.GetConversationMessagesAsync(tenantId, userId, conversationId, includeContextMarkers, limit, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeamAgentMessageDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{conversationId:long}")]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long conversationId, [FromBody] TeamAgentConversationUpdateRequest request, CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _teamAgentService.UpdateConversationAsync(tenantId, userId, conversationId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = conversationId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{conversationId:long}")]
    [Authorize(Policy = PermissionPolicies.ConversationDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long conversationId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _teamAgentService.DeleteConversationAsync(tenantId, userId, conversationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = conversationId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{conversationId:long}/clear-context")]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> ClearContext(long conversationId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _teamAgentService.ClearConversationContextAsync(tenantId, userId, conversationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = conversationId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{conversationId:long}/clear-history")]
    [Authorize(Policy = PermissionPolicies.ConversationDelete)]
    public async Task<ActionResult<ApiResponse<object>>> ClearHistory(long conversationId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _teamAgentService.ClearConversationHistoryAsync(tenantId, userId, conversationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = conversationId.ToString() }, HttpContext.TraceIdentifier));
    }
}
