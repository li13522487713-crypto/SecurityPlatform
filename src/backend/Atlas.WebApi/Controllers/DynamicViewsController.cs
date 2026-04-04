using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/dynamic-views")]
[PlatformOnly]
public sealed class DynamicViewsController : ControllerBase
{
    private readonly IDynamicViewQueryService _queryService;
    private readonly IDynamicViewCommandService _commandService;
    private readonly IDynamicDeleteCheckService _deleteCheckService;
    private readonly IDynamicViewP2Service _p2Service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;

    public DynamicViewsController(
        IDynamicViewQueryService queryService,
        IDynamicViewCommandService commandService,
        IDynamicDeleteCheckService deleteCheckService,
        IDynamicViewP2Service p2Service,
        ITenantProvider tenantProvider,
        IAppContextAccessor appContextAccessor,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder)
    {
        _queryService = queryService;
        _commandService = commandService;
        _deleteCheckService = deleteCheckService;
        _p2Service = p2Service;
        _tenantProvider = tenantProvider;
        _appContextAccessor = appContextAccessor;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<DynamicViewListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, _appContextAccessor.ResolveAppId(), request, cancellationToken);
        return Ok(ApiResponse<PagedResult<DynamicViewListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{viewKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicViewDefinitionDto?>>> GetByKey(string viewKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByKeyAsync(tenantId, _appContextAccessor.ResolveAppId(), viewKey, cancellationToken);
        return Ok(ApiResponse<DynamicViewDefinitionDto?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] DynamicViewCreateOrUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var viewKey = await _commandService.CreateAsync(tenantId, user.UserId, request, cancellationToken);
        await RecordAuditAsync(user, "CREATE_DYNAMIC_VIEW", viewKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ViewKey = viewKey }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{viewKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(string viewKey, [FromBody] DynamicViewCreateOrUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, user.UserId, viewKey, request, cancellationToken);
        await RecordAuditAsync(user, "UPDATE_DYNAMIC_VIEW", viewKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ViewKey = viewKey }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{viewKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string viewKey, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), viewKey, cancellationToken);
        await RecordAuditAsync(user, "DELETE_DYNAMIC_VIEW", viewKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ViewKey = viewKey }, HttpContext.TraceIdentifier));
    }

    [HttpPost("preview")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<Atlas.Application.DynamicTables.Models.DynamicRecordListResult>>> Preview([FromBody] DynamicViewPreviewRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.PreviewAsync(tenantId, _appContextAccessor.ResolveAppId(), request, cancellationToken);
        return Ok(ApiResponse<Atlas.Application.DynamicTables.Models.DynamicRecordListResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("preview-sql")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicViewSqlPreviewDto>>> PreviewSql([FromBody] DynamicViewSqlPreviewRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.PreviewSqlAsync(tenantId, _appContextAccessor.ResolveAppId(), request, cancellationToken);
        return Ok(ApiResponse<DynamicViewSqlPreviewDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{viewKey}/publish")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicViewPublishResultDto>>> Publish(string viewKey, [FromBody] Dictionary<string, string?>? body, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicViewPublishResultDto>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.PublishAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), viewKey, body?.GetValueOrDefault("comment"), cancellationToken);
        await RecordAuditAsync(user, "PUBLISH_DYNAMIC_VIEW", viewKey, cancellationToken);
        return Ok(ApiResponse<DynamicViewPublishResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{viewKey}/history")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicViewHistoryItemDto>>>> History(string viewKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetHistoryAsync(tenantId, _appContextAccessor.ResolveAppId(), viewKey, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicViewHistoryItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{viewKey}/rollback/{version:int}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicViewPublishResultDto>>> Rollback(string viewKey, int version, [FromBody] Dictionary<string, string?>? body, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicViewPublishResultDto>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.RollbackAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), viewKey, version, body?.GetValueOrDefault("comment"), cancellationToken);
        return Ok(ApiResponse<DynamicViewPublishResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{viewKey}/records/query")]
    [Authorize(Policy = PermissionPolicies.AppUser)]
    public async Task<ActionResult<ApiResponse<Atlas.Application.DynamicTables.Models.DynamicRecordListResult>>> QueryRecords(
        string viewKey,
        [FromBody] DynamicViewRecordsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryRecordsAsync(tenantId, _appContextAccessor.ResolveAppId(), viewKey, request, cancellationToken);
        return Ok(ApiResponse<Atlas.Application.DynamicTables.Models.DynamicRecordListResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{viewKey}/references")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DeleteCheckBlockerDto>>>> References(string viewKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetReferencesAsync(tenantId, _appContextAccessor.ResolveAppId(), viewKey, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DeleteCheckBlockerDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{viewKey}/delete-check")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DeleteCheckResultDto>>> DeleteCheck(string viewKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _deleteCheckService.CheckViewDeleteAsync(tenantId, _appContextAccessor.ResolveAppId(), viewKey, cancellationToken);
        return Ok(ApiResponse<DeleteCheckResultDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("external-extract/preview")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicExternalExtractPreviewResult>>> PreviewExternalExtract(
        [FromBody] DynamicExternalExtractPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicExternalExtractPreviewResult>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var appId = _appContextAccessor.ResolveAppId() ?? 0;
        var result = await _p2Service.PreviewExternalExtractAsync(tenantId, appId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<DynamicExternalExtractPreviewResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("external-extract/data-sources")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicExternalExtractDataSourceDto>>>> ListExternalExtractDataSources(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var appId = _appContextAccessor.ResolveAppId();
        if (!appId.HasValue)
        {
            return BadRequest(ApiResponse<IReadOnlyList<DynamicExternalExtractDataSourceDto>>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "AppIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var result = await _p2Service.ListExternalExtractDataSourcesAsync(tenantId, appId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicExternalExtractDataSourceDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("external-extract/{dataSourceId:long}/schema")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicExternalExtractSchemaResult>>> GetExternalExtractSchema(long dataSourceId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var appId = _appContextAccessor.ResolveAppId();
        if (!appId.HasValue)
        {
            return BadRequest(ApiResponse<DynamicExternalExtractSchemaResult>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "AppIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var result = await _p2Service.GetExternalExtractSchemaAsync(tenantId, appId.Value, dataSourceId, cancellationToken);
        return Ok(ApiResponse<DynamicExternalExtractSchemaResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{viewKey}/publish-physical")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicPhysicalViewPublishResult>>> PublishPhysical(
        string viewKey,
        [FromBody] DynamicPhysicalViewPublishRequest request,
        CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicPhysicalViewPublishResult>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _p2Service.PublishPhysicalViewAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), viewKey, request, cancellationToken);
        return Ok(ApiResponse<DynamicPhysicalViewPublishResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{viewKey}/physical-publications")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicPhysicalViewPublicationDto>>>> ListPhysicalPublications(
        string viewKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _p2Service.ListPhysicalPublicationsAsync(tenantId, _appContextAccessor.ResolveAppId(), viewKey, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicPhysicalViewPublicationDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{viewKey}/physical-rollback/{version:int}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicPhysicalViewPublishResult>>> RollbackPhysical(
        string viewKey,
        int version,
        CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<DynamicPhysicalViewPublishResult>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _p2Service.RollbackPhysicalPublicationAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), viewKey, version, cancellationToken);
        return Ok(ApiResponse<DynamicPhysicalViewPublishResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{viewKey}/physical-publications/{publicationId}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> DeletePhysicalPublication(
        string viewKey,
        string publicationId,
        CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _p2Service.DeletePhysicalPublicationAsync(tenantId, user.UserId, _appContextAccessor.ResolveAppId(), viewKey, publicationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ViewKey = viewKey, PublicationId = publicationId }, HttpContext.TraceIdentifier));
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

