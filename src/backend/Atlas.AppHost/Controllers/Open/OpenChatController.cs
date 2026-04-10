using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Integration;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Helpers;
using Atlas.Presentation.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Atlas.Presentation.Shared.Filters;
using Atlas.Presentation.Shared.Helpers;

namespace Atlas.AppHost.Controllers.Open;

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
            await TypedResults.Json(ApiResponse<object>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:chat 权限",
                HttpContext.TraceIdentifier), statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(HttpContext);
            return;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdSafely(User)
            ?? throw new UnauthorizedAccessException("缺少用户标识");
        var chatRequest = new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag, request.Attachments);
        var useStructuredEvents = SseStreamHelper.ShouldUseStructuredEvents(Request);

        IResult streamResult;
        if (!useStructuredEvents)
        {
            streamResult = TypedResults.ServerSentEvents(
                SseStreamHelper.AppendDone(
                    _chatService.ChatStreamAsync(
                        tenantId,
                        userId,
                        request.AgentId,
                        chatRequest,
                        cancellationToken),
                    cancellationToken: cancellationToken));
        }
        else
        {
            var stream = SseStreamHelper.AppendDone(
                SseStreamHelper.ToSseItems(
                    _chatService.ChatEventStreamAsync(
                        tenantId,
                        userId,
                        request.AgentId,
                        chatRequest,
                        cancellationToken),
                    evt => evt.EventType,
                    evt => evt.Data,
                    cancellationToken),
                cancellationToken: cancellationToken);
            streamResult = TypedResults.ServerSentEvents(stream);
        }

        await streamResult.ExecuteAsync(HttpContext);
        await TryDispatchAgentMessageEventAsync(userId, request, result: null, cancellationToken);
    }

    public sealed record OpenChatRequest(
        long AgentId,
        string Message,
        long? ConversationId,
        bool? EnableRag,
        IReadOnlyList<AgentChatAttachment>? Attachments);

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
