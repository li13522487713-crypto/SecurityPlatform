using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/apps/{appId:long}/schema-drafts")]
public sealed class SchemaDraftsController : ControllerBase
{
    private readonly ISchemaDraftService _schemaDraftService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;

    public SchemaDraftsController(
        ISchemaDraftService schemaDraftService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder)
    {
        _schemaDraftService = schemaDraftService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SchemaDraftListItem>>>> List(
        long appId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var drafts = await _schemaDraftService.ListDraftsAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SchemaDraftListItem>>.Ok(drafts, HttpContext.TraceIdentifier));
    }

    [HttpGet("{draftId:long}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaDraftListItem?>>> GetById(
        long appId,
        long draftId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var draft = await _schemaDraftService.GetDraftAsync(tenantId, draftId, cancellationToken);
        return Ok(ApiResponse<SchemaDraftListItem?>.Ok(draft, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        long appId,
        [FromBody] DynamicSchemaDraftCreateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _schemaDraftService.CreateDraftAsync(tenantId, currentUser.UserId, request, cancellationToken);
        await RecordAuditAsync(currentUser, "CREATE_SCHEMA_DRAFT", $"{appId}/{request.ObjectKey}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{draftId:long}/validate")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Validate(
        long appId,
        long draftId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _schemaDraftService.ValidateDraftAsync(tenantId, draftId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { DraftId = draftId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("publish")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<SchemaDraftPublishResult>>> Publish(
        long appId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<SchemaDraftPublishResult>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _schemaDraftService.PublishDraftsAsync(tenantId, currentUser.UserId, appId, cancellationToken);
        await RecordAuditAsync(currentUser, "PUBLISH_SCHEMA_DRAFTS", appId.ToString(), cancellationToken);
        return Ok(ApiResponse<SchemaDraftPublishResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{draftId:long}/abandon")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Abandon(
        long appId,
        long draftId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _schemaDraftService.AbandonDraftAsync(tenantId, draftId, cancellationToken);
        await RecordAuditAsync(currentUser, "ABANDON_SCHEMA_DRAFT", draftId.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { DraftId = draftId.ToString() }, HttpContext.TraceIdentifier));
    }

    private Task RecordAuditAsync(
        CurrentUserInfo currentUser,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var context = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        return _auditRecorder.RecordAsync(context, cancellationToken);
    }
}
