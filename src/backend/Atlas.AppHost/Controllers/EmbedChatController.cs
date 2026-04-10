using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Atlas.Presentation.Shared.Filters;
using Atlas.Presentation.Shared.Helpers;

namespace Atlas.AppHost.Controllers;

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

        var chatRequest = new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag, request.Attachments);
        var useStructuredEvents = SseStreamHelper.ShouldUseStructuredEvents(Request);

        IResult streamResult;
        if (!useStructuredEvents)
        {
            streamResult = TypedResults.ServerSentEvents(
                SseStreamHelper.AppendDone(
                    _agentChatService.ChatStreamAsync(
                        publication.TenantId,
                        embedUserId,
                        publication.AgentId,
                        chatRequest,
                        cancellationToken),
                    cancellationToken: cancellationToken));
        }
        else
        {
            var stream = SseStreamHelper.AppendDone(
                SseStreamHelper.ToSseItems(
                    _agentChatService.ChatEventStreamAsync(
                        publication.TenantId,
                        embedUserId,
                        publication.AgentId,
                        chatRequest,
                        cancellationToken),
                    evt => evt.EventType,
                    evt => evt.Data,
                    cancellationToken),
                cancellationToken: cancellationToken);
            streamResult = TypedResults.ServerSentEvents(stream);
        }

        await streamResult.ExecuteAsync(HttpContext);
    }

    public sealed record EmbedChatRequest(
        string EmbedToken,
        string Message,
        long? ConversationId,
        bool? EnableRag,
        string? ExternalUserId,
        IReadOnlyList<AgentChatAttachment>? Attachments);

    private static long ResolveEmbedUserId(long publicationId, string? externalUserId)
    {
        var seed = $"{publicationId}:{externalUserId?.Trim() ?? "anonymous"}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var value = (long)(BitConverter.ToUInt64(hash, 0) & long.MaxValue);
        return value == 0 ? 1 : value;
    }
}
