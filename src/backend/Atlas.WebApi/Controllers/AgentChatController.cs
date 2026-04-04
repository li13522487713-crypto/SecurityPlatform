using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/agents/{agentId:long}/chat")]
[AppRuntimeOnly]
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
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var useStructuredEvents = ShouldUseStructuredEvents(Request);

        if (!useStructuredEvents)
        {
            await foreach (var chunk in _agentChatService.ChatStreamAsync(tenantId, userId, agentId, request, cancellationToken))
            {
                await WriteSseDataEventAsync(Response, chunk, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        else
        {
            await foreach (var evt in _agentChatService.ChatEventStreamAsync(tenantId, userId, agentId, request, cancellationToken))
            {
                await WriteSseTypedEventAsync(Response, evt.EventType, evt.Data, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }

        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
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

    private static async Task WriteSseDataEventAsync(HttpResponse response, string payload, CancellationToken cancellationToken)
    {
        var normalized = payload.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        foreach (var line in lines)
        {
            await response.WriteAsync($"data: {line}\n", cancellationToken);
        }

        await response.WriteAsync("\n", cancellationToken);
    }

    private static async Task WriteSseTypedEventAsync(
        HttpResponse response,
        string eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventType}\n", cancellationToken);
        await WriteSseDataEventAsync(response, payload, cancellationToken);
    }

    private static bool ShouldUseStructuredEvents(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Stream-Event-Mode", out var mode) &&
            mode.Count > 0 &&
            string.Equals(mode[0], "react", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Query.TryGetValue("eventMode", out var eventMode) &&
            eventMode.Count > 0 &&
            string.Equals(eventMode[0], "react", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
