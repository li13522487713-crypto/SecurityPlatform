using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Presentation.Shared.Controllers.Ai;

[ApiController]
[Route("api/v1/agents/{agentId:long}/chat")]
public sealed class AgentChatController : ControllerBase
{
    private readonly IAgentChatService _agentChatService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AgentChatRequest> _chatValidator;
    private readonly IValidator<AgentChatCancelRequest> _cancelValidator;

    public AgentChatController(
        IAgentChatService agentChatService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AgentChatRequest> chatValidator,
        IValidator<AgentChatCancelRequest> cancelValidator)
    {
        _agentChatService = agentChatService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _chatValidator = chatValidator;
        _cancelValidator = cancelValidator;
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AgentChatResponse>>> Chat(
        long agentId,
        [FromBody] AgentChatRequest request,
        CancellationToken cancellationToken)
    {
        _chatValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _agentChatService.ChatAsync(tenantId, userId, agentId, request, cancellationToken);
        return Ok(ApiResponse<AgentChatResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("stream")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task ChatStream(
        long agentId,
        [FromBody] AgentChatRequest request,
        CancellationToken cancellationToken)
    {
        _chatValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var useStructuredEvents = SseStreamHelper.ShouldUseStructuredEvents(Request);

        IResult streamResult;
        if (!useStructuredEvents)
        {
            streamResult = TypedResults.ServerSentEvents(
                SseStreamHelper.AppendDone(
                    _agentChatService.ChatStreamAsync(tenantId, userId, agentId, request, cancellationToken),
                    cancellationToken: cancellationToken));
        }
        else
        {
            var stream = SseStreamHelper.AppendDone(
                SseStreamHelper.ToSseItems(
                    _agentChatService.ChatEventStreamAsync(tenantId, userId, agentId, request, cancellationToken),
                    evt => evt.EventType,
                    evt => evt.Data,
                    cancellationToken),
                cancellationToken: cancellationToken);
            streamResult = TypedResults.ServerSentEvents(stream);
        }

        await streamResult.ExecuteAsync(HttpContext);
    }

    [HttpPost("cancel")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(
        long agentId,
        [FromBody] AgentChatCancelRequest request,
        CancellationToken cancellationToken)
    {
        _cancelValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _agentChatService.CancelAsync(tenantId, userId, agentId, request.ConversationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = request.ConversationId.ToString() }, HttpContext.TraceIdentifier));
    }
}
