using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/team-agents")]
public sealed class TeamAgentsController : ControllerBase
{
    private readonly ITeamAgentService _teamAgentService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IValidator<TeamAgentCreateRequest> _createValidator;
    private readonly IValidator<TeamAgentUpdateRequest> _updateValidator;
    private readonly IValidator<TeamAgentCreateFromTemplateRequest> _createFromTemplateValidator;
    private readonly IValidator<TeamAgentConversationCreateRequest> _conversationCreateValidator;
    private readonly IValidator<TeamAgentChatRequest> _chatValidator;
    private readonly IValidator<TeamAgentChatCancelRequest> _chatCancelValidator;
    private readonly IValidator<SchemaDraftCreateRequest> _draftCreateValidator;
    private readonly IValidator<SchemaDraftUpdateRequest> _draftUpdateValidator;
    private readonly IValidator<SchemaDraftConfirmationRequest> _draftConfirmationValidator;
    private readonly IValidator<TeamAgentPublicationPublishRequest> _publishValidator;
    private readonly IValidator<TeamAgentLegacyMigrationRequest> _legacyMigrationValidator;

    public TeamAgentsController(
        ITeamAgentService teamAgentService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor,
        IValidator<TeamAgentCreateRequest> createValidator,
        IValidator<TeamAgentUpdateRequest> updateValidator,
        IValidator<TeamAgentCreateFromTemplateRequest> createFromTemplateValidator,
        IValidator<TeamAgentConversationCreateRequest> conversationCreateValidator,
        IValidator<TeamAgentChatRequest> chatValidator,
        IValidator<TeamAgentChatCancelRequest> chatCancelValidator,
        IValidator<SchemaDraftCreateRequest> draftCreateValidator,
        IValidator<SchemaDraftUpdateRequest> draftUpdateValidator,
        IValidator<SchemaDraftConfirmationRequest> draftConfirmationValidator,
        IValidator<TeamAgentPublicationPublishRequest> publishValidator,
        IValidator<TeamAgentLegacyMigrationRequest> legacyMigrationValidator)
    {
        _teamAgentService = teamAgentService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _appContextAccessor = appContextAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _createFromTemplateValidator = createFromTemplateValidator;
        _conversationCreateValidator = conversationCreateValidator;
        _chatValidator = chatValidator;
        _chatCancelValidator = chatCancelValidator;
        _draftCreateValidator = draftCreateValidator;
        _draftUpdateValidator = draftUpdateValidator;
        _draftConfirmationValidator = draftConfirmationValidator;
        _publishValidator = publishValidator;
        _legacyMigrationValidator = legacyMigrationValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TeamAgentListItem>>>> Get(
        [FromQuery] string? keyword,
        [FromQuery] TeamAgentMode? teamMode,
        [FromQuery] TeamAgentStatus? status,
        [FromQuery] string? capabilityTag,
        [FromQuery] string? defaultEntrySkill,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.GetPagedAsync(tenantId, keyword, teamMode, status, capabilityTag, defaultEntrySkill, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<TeamAgentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<TeamAgentDashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.GetDashboardAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<TeamAgentDashboardDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<TeamAgentDetail?>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.GetByIdAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<TeamAgentDetail?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] TeamAgentCreateRequest request, CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _teamAgentService.CreateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, [FromBody] TeamAgentUpdateRequest request, CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _teamAgentService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _teamAgentService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/duplicate")]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Duplicate(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var duplicatedId = await _teamAgentService.DuplicateAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = duplicatedId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(long id, [FromBody] TeamAgentPublicationPublishRequest request, CancellationToken cancellationToken)
    {
        _publishValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _teamAgentService.PublishAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("templates")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamAgentTemplateItem>>>> GetTemplates(CancellationToken cancellationToken)
    {
        var result = await _teamAgentService.GetTemplatesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeamAgentTemplateItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("from-template")]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateFromTemplate([FromBody] TeamAgentCreateFromTemplateRequest request, CancellationToken cancellationToken)
    {
        _createFromTemplateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _teamAgentService.CreateFromTemplateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("migrations/multi-agent-orchestrations")]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<TeamAgentLegacyMigrationResult>>> MigrateLegacy(
        [FromBody] TeamAgentLegacyMigrationRequest request,
        CancellationToken cancellationToken)
    {
        _legacyMigrationValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _teamAgentService.MigrateLegacyAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<TeamAgentLegacyMigrationResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("migrations/multi-agent-orchestrations/status")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<TeamAgentLegacyMigrationStatusDto>>> GetLegacyMigrationStatus(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.GetLegacyMigrationStatusAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<TeamAgentLegacyMigrationStatusDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/conversations")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<TeamAgentConversationDto>>>> GetConversations(
        long id,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _teamAgentService.ListConversationsAsync(tenantId, id, userId, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<TeamAgentConversationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/conversations")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> CreateConversation(long id, [FromBody] TeamAgentConversationCreateRequest request, CancellationToken cancellationToken)
    {
        _conversationCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var conversationId = await _teamAgentService.CreateConversationAsync(tenantId, id, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = conversationId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/chat")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<TeamAgentChatResponse>>> Chat(long id, [FromBody] TeamAgentChatRequest request, CancellationToken cancellationToken)
    {
        _chatValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _teamAgentService.ChatAsync(tenantId, userId, id, request, _appContextAccessor.GetAppId(), cancellationToken);
        return Ok(ApiResponse<TeamAgentChatResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/chat/stream")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task ChatStream(long id, [FromBody] TeamAgentChatRequest request, CancellationToken cancellationToken)
    {
        _chatValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var stream = SseStreamHelper.AppendDone(
            SseStreamHelper.ToSseItems(
                _teamAgentService.ChatStreamAsync(tenantId, userId, id, request, _appContextAccessor.GetAppId(), cancellationToken),
                evt => evt.EventType,
                evt => evt.Data,
                cancellationToken),
            cancellationToken: cancellationToken);
        var streamResult = TypedResults.ServerSentEvents(stream);
        await streamResult.ExecuteAsync(HttpContext);
    }

    [HttpPost("{id:long}/chat/cancel")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> CancelChat(long id, [FromBody] TeamAgentChatCancelRequest request, CancellationToken cancellationToken)
    {
        _chatCancelValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _teamAgentService.CancelChatAsync(tenantId, userId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = request.ConversationId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{executionId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<TeamAgentExecutionResult?>>> GetExecution(long executionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.GetExecutionAsync(tenantId, executionId, cancellationToken);
        return Ok(ApiResponse<TeamAgentExecutionResult?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/schema-drafts")]
    [Authorize(Policy = PermissionPolicies.ConversationCreate)]
    public async Task<ActionResult<ApiResponse<object>>> CreateSchemaDraft(long id, [FromBody] SchemaDraftCreateRequest request, CancellationToken cancellationToken)
    {
        _draftCreateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var draftId = await _teamAgentService.CreateSchemaDraftAsync(tenantId, id, userId, request, _appContextAccessor.GetAppId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = draftId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/schema-drafts")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamAgentSchemaDraftListItem>>>> GetSchemaDrafts(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.ListSchemaDraftsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeamAgentSchemaDraftListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/schema-drafts/{draftId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<TeamAgentSchemaDraftDetail?>>> GetSchemaDraft(long id, long draftId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.GetSchemaDraftAsync(tenantId, id, draftId, cancellationToken);
        return Ok(ApiResponse<TeamAgentSchemaDraftDetail?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/schema-drafts/{draftId:long}/execution-audits")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamAgentSchemaDraftExecutionAuditItem>>>> GetSchemaDraftExecutionAudits(
        long id,
        long draftId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _teamAgentService.GetSchemaDraftExecutionAuditsAsync(tenantId, id, draftId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeamAgentSchemaDraftExecutionAuditItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/schema-drafts/{draftId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSchemaDraft(long id, long draftId, [FromBody] SchemaDraftUpdateRequest request, CancellationToken cancellationToken)
    {
        _draftUpdateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _teamAgentService.UpdateSchemaDraftAsync(tenantId, id, draftId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = draftId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/schema-drafts/{draftId:long}/confirm-create")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<SchemaDraftConfirmationResponse>>> ConfirmSchemaDraft(long id, long draftId, [FromBody] SchemaDraftConfirmationRequest request, CancellationToken cancellationToken)
    {
        _draftConfirmationValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _teamAgentService.ConfirmSchemaDraftAsync(tenantId, id, draftId, userId, request, cancellationToken);
        return Ok(ApiResponse<SchemaDraftConfirmationResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/schema-drafts/{draftId:long}/discard")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DiscardSchemaDraft(long id, long draftId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _teamAgentService.DiscardSchemaDraftAsync(tenantId, id, draftId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = draftId.ToString() }, HttpContext.TraceIdentifier));
    }
}
