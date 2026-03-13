using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers.Open;

[ApiController]
[Route("api/v1/open/chat")]
[Authorize(AuthenticationSchemes = PatAuthenticationHandler.SchemeName)]
public sealed class OpenChatController : ControllerBase
{
    private readonly IAgentChatService _chatService;
    private readonly ITenantProvider _tenantProvider;

    public OpenChatController(IAgentChatService chatService, ITenantProvider tenantProvider)
    {
        _chatService = chatService;
        _tenantProvider = tenantProvider;
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
            new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag),
            cancellationToken);
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

        await foreach (var chunk in _chatService.ChatStreamAsync(
                           tenantId,
                           userId,
                           request.AgentId,
                           new AgentChatRequest(request.ConversationId, request.Message, request.EnableRag),
                           cancellationToken))
        {
            await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    public sealed record OpenChatRequest(
        long AgentId,
        string Message,
        long? ConversationId,
        bool? EnableRag);
}
