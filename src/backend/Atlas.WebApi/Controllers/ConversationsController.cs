using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<ConversationCreateRequest> _createValidator;
    private readonly IValidator<ConversationUpdateRequest> _updateValidator;

    public ConversationsController(
        IConversationService conversationService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<ConversationCreateRequest> createValidator,
        IValidator<ConversationUpdateRequest> updateValidator)
    {
        _conversationService = conversationService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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
            return NotFound(ApiResponse<ConversationDto>.Fail(ErrorCodes.NotFound, "会话不存在", HttpContext.TraceIdentifier));
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
