using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/embed-chat")]
[AllowAnonymous]
public sealed class EmbedChatController : ControllerBase
{
    private readonly IAgentPublicationService _publicationService;
    private readonly IAgentChatService _agentChatService;

    public EmbedChatController(
        IAgentPublicationService publicationService,
        IAgentChatService agentChatService)
    {
        _publicationService = publicationService;
        _agentChatService = agentChatService;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<AgentChatResponse>>> Chat(
        [FromBody] EmbedChatRequest request,
        CancellationToken cancellationToken)
    {
        var publication = await _publicationService.ResolveByEmbedTokenAsync(request.EmbedToken, cancellationToken);
        var embedUserId = ResolveEmbedUserId(publication.PublicationId, request.ExternalUserId);
        var result = await _agentChatService.ChatAsync(
            publication.TenantId,
            embedUserId,
            publication.AgentId,
            new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag, request.Attachments),
            cancellationToken);
        return Ok(ApiResponse<AgentChatResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("stream")]
    public async Task ChatStream(
        [FromBody] EmbedChatRequest request,
        CancellationToken cancellationToken)
    {
        var publication = await _publicationService.ResolveByEmbedTokenAsync(request.EmbedToken, cancellationToken);
        var embedUserId = ResolveEmbedUserId(publication.PublicationId, request.ExternalUserId);

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        var useStructuredEvents = ShouldUseStructuredEvents(Request);

        if (!useStructuredEvents)
        {
            await foreach (var chunk in _agentChatService.ChatStreamAsync(
                               publication.TenantId,
                               embedUserId,
                               publication.AgentId,
                               new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag, request.Attachments),
                               cancellationToken))
            {
                await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        else
        {
            await foreach (var evt in _agentChatService.ChatEventStreamAsync(
                               publication.TenantId,
                               embedUserId,
                               publication.AgentId,
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
    }

    public sealed record EmbedChatRequest(
        string EmbedToken,
        string Message,
        long? ConversationId,
        bool? EnableRag,
        string? ExternalUserId,
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

    private static long ResolveEmbedUserId(long publicationId, string? externalUserId)
    {
        var seed = $"{publicationId}:{externalUserId?.Trim() ?? "anonymous"}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var value = (long)(BitConverter.ToUInt64(hash, 0) & long.MaxValue);
        return value == 0 ? 1 : value;
    }
}
