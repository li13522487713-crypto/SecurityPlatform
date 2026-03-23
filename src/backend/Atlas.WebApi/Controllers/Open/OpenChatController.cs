using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Integration;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Atlas.WebApi.Controllers.Open;

[ApiController]
[Route("api/v1/open/chat")]
[Authorize(AuthenticationSchemes = $"{PatAuthenticationHandler.SchemeName},{OpenProjectAuthenticationHandler.SchemeName}")]
public sealed class OpenChatController : ControllerBase
{
    private readonly IAgentChatService _chatService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IWebhookService _webhookService;
    private readonly ILogger<OpenChatController> _logger;

    public OpenChatController(
        IAgentChatService chatService,
        ITenantProvider tenantProvider,
        IWebhookService webhookService,
        ILogger<OpenChatController> logger)
    {
        _chatService = chatService;
        _tenantProvider = tenantProvider;
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost("completions")]
    public async Task<ActionResult<ApiResponse<AgentChatResponse>>> Chat(
        [FromBody] OpenChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:chat"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AgentChatResponse>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:chat 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdSafely(User)
            ?? throw new UnauthorizedAccessException("缺少用户标识");
        var result = await _chatService.ChatAsync(
            tenantId,
            userId,
            request.AgentId,
            new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag, request.Attachments),
            cancellationToken);
        await TryDispatchAgentMessageEventAsync(userId, request, result, cancellationToken);
        return Ok(ApiResponse<AgentChatResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("stream")]
    public async Task ChatStream(
        [FromBody] OpenChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:chat"))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:chat 权限",
                HttpContext.TraceIdentifier), cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdSafely(User)
            ?? throw new UnauthorizedAccessException("缺少用户标识");
        var useStructuredEvents = ShouldUseStructuredEvents(Request);

        if (!useStructuredEvents)
        {
            await foreach (var chunk in _chatService.ChatStreamAsync(
                               tenantId,
                               userId,
                               request.AgentId,
                               new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag, request.Attachments),
                               cancellationToken))
            {
                await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        else
        {
            await foreach (var evt in _chatService.ChatEventStreamAsync(
                               tenantId,
                               userId,
                               request.AgentId,
                               new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag, request.Attachments),
                               cancellationToken))
            {
                await Response.WriteAsync($"event: {evt.EventType}\n", cancellationToken);
                await Response.WriteAsync($"data: {evt.Data}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }

        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
        await TryDispatchAgentMessageEventAsync(userId, request, result: null, cancellationToken);
    }

    public sealed record OpenChatRequest(
        long AgentId,
        string Message,
        long? ConversationId,
        bool? EnableRag,
        IReadOnlyList<AgentChatAttachment>? Attachments);

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

    private async Task TryDispatchAgentMessageEventAsync(
        long userId,
        OpenChatRequest request,
        AgentChatResponse? result,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                eventType = "agent.message",
                occurredAt = DateTimeOffset.UtcNow,
                agentId = request.AgentId,
                userId,
                conversationId = result?.ConversationId ?? request.ConversationId,
                message = request.Message,
                responseLength = result?.Content?.Length ?? 0
            });
            await _webhookService.DispatchAsync("agent.message", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open chat webhook dispatch failed.");
        }
    }
}
