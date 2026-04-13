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

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<ConversationCreateRequest> _createValidator;
    private readonly IValidator<ConversationUpdateRequest> _updateValidator;
    private readonly IValidator<ConversationAppendMessageRequest> _appendValidator;

    public ConversationsController(
        IConversationService conversationService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<ConversationCreateRequest> createValidator,
        IValidator<ConversationUpdateRequest> updateValidator,
        IValidator<ConversationAppendMessageRequest> appendValidator)
    {
        _conversationService = conversationService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _appendValidator = appendValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ConversationView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ConversationDto>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] long? agentId = null,
        [FromQuery] long? userId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUserId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        if (userId.HasValue && userId.Value != currentUserId)
        {
            return Forbid();
        }

        PagedResult<ConversationDto> result;
        if (agentId.HasValue && agentId.Value > 0)
        {
            result = await _conversationService.ListByAgentAsync(
                tenantId,
                agentId.Value,
                currentUserId,
                request.PageIndex,
                request.PageSize,
                cancellationToken);
        }
        else
        {
            result = await _conversationService.ListByUserAsync(
                tenantId,
                currentUserId,
                request.PageIndex,
                request.PageSize,
                cancellationToken);
        }

        return Ok(ApiResponse<PagedResult<ConversationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ConversationView)]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _conversationService.GetByIdAsync(tenantId, userId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "ConversationNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ConversationDto>.Ok(result, HttpContext.TraceIdentifier));
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

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] ConversationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _conversationService.UpdateAsync(tenantId, userId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ConversationDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _conversationService.DeleteAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/clear-context")]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> ClearContext(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _conversationService.ClearContextAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/clear-history")]
    [Authorize(Policy = PermissionPolicies.ConversationDelete)]
    public async Task<ActionResult<ApiResponse<object>>> ClearHistory(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _conversationService.ClearHistoryAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/messages")]
    [Authorize(Policy = PermissionPolicies.ConversationView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChatMessageDto>>>> GetMessages(
        long id,
        [FromQuery] bool includeContextMarkers = false,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _conversationService.GetMessagesAsync(
            tenantId,
            userId,
            id,
            includeContextMarkers,
            limit,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChatMessageDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/messages")]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> AppendMessage(
        long id,
        [FromBody] ConversationAppendMessageRequest request,
        CancellationToken cancellationToken)
    {
        _appendValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var messageId = await _conversationService.AppendMessageAsync(tenantId, userId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = messageId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/messages/{msgId:long}")]
    [Authorize(Policy = PermissionPolicies.ConversationDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMessage(
        long id,
        long msgId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _conversationService.DeleteMessageAsync(tenantId, userId, id, msgId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = msgId.ToString() }, HttpContext.TraceIdentifier));
    }
}
