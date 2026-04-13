using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/agent-sessions")]
public sealed class AgentSessionsController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<ConversationCreateRequest> _createValidator;
    private readonly IValidator<ConversationAppendMessageRequest> _appendValidator;

    public AgentSessionsController(
        IConversationService conversationService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<ConversationCreateRequest> createValidator,
        IValidator<ConversationAppendMessageRequest> appendValidator)
    {
        _conversationService = conversationService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _appendValidator = appendValidator;
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] ConversationCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _conversationService.CreateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{sessionId:long}/messages")]
    [Authorize(Policy = PermissionPolicies.ConversationView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChatMessageDto>>>> GetMessages(
        long sessionId,
        [FromQuery] bool includeContextMarkers = false,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _conversationService.GetMessagesAsync(
            tenantId,
            userId,
            sessionId,
            includeContextMarkers,
            limit,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChatMessageDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{sessionId:long}/messages")]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> AppendMessage(
        long sessionId,
        [FromBody] ConversationAppendMessageRequest request,
        CancellationToken cancellationToken)
    {
        _appendValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _conversationService.AppendMessageAsync(tenantId, userId, sessionId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
