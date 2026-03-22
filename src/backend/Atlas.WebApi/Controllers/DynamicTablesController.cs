using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/dynamic-tables")]
public sealed class DynamicTablesController : ControllerBase
{
    private readonly IDynamicTableQueryService _queryService;
    private readonly IDynamicTableCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IValidator<DynamicTableCreateRequest> _createValidator;
    private readonly IValidator<DynamicTableUpdateRequest> _updateValidator;

    public DynamicTablesController(
        IDynamicTableQueryService queryService,
        IDynamicTableCommandService commandService,
        ITenantProvider tenantProvider,
        IAppContextAccessor appContextAccessor,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        IValidator<DynamicTableCreateRequest> createValidator,
        IValidator<DynamicTableUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _appContextAccessor = appContextAccessor;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<DynamicTableListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(request, tenantId, _appContextAccessor.ResolveAppId(), cancellationToken);
        return Ok(ApiResponse<PagedResult<DynamicTableListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTableDetail?>>> GetByKey(
        string tableKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableKey))
        {
            return BadRequest(ApiResponse<DynamicTableDetail?>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TableViewKeyRequired"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        return Ok(ApiResponse<DynamicTableDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}/fields")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicFieldDefinition>>>> GetFields(
        string tableKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableKey))
        {
            return BadRequest(ApiResponse<IReadOnlyList<DynamicFieldDefinition>>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TableViewKeyRequired"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var fields = await _queryService.GetFieldsAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicFieldDefinition>>.Ok(fields, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}/relations")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicRelationDefinition>>>> GetRelations(
        string tableKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableKey))
        {
            return BadRequest(ApiResponse<IReadOnlyList<DynamicRelationDefinition>>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TableViewKeyRequired"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var relations = await _queryService.GetRelationsAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicRelationDefinition>>.Ok(relations, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}/field-permissions")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DynamicFieldPermissionRule>>>> GetFieldPermissions(
        string tableKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableKey))
        {
            return BadRequest(ApiResponse<IReadOnlyList<DynamicFieldPermissionRule>>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TableViewKeyRequired"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var rules = await _queryService.GetFieldPermissionsAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicFieldPermissionRule>>.Ok(rules, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] DynamicTableCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        await RecordAuditAsync(currentUser, "CREATE_DYNAMIC_TABLE", request.TableKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{tableKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        string tableKey,
        [FromBody] DynamicTableUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                ApiResponseLocalizer.T(HttpContext, "UserNotSignedIn"),
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        await RecordAuditAsync(currentUser, "UPDATE_DYNAMIC_TABLE", tableKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{tableKey}/schema/alter")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> AlterSchema(
        string tableKey,
        [FromBody] DynamicTableAlterRequest request,
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
        await _commandService.AlterAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        await RecordAuditAsync(currentUser, "ALTER_DYNAMIC_TABLE_SCHEMA", tableKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{tableKey}/schema/alter/preview")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<DynamicTableAlterPreviewResponse>>> PreviewAlterSchema(
        string tableKey,
        [FromBody] DynamicTableAlterRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var preview = await _commandService.PreviewAlterAsync(tenantId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<DynamicTableAlterPreviewResponse>.Ok(preview, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{tableKey}")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string tableKey,
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
        await _commandService.DeleteAsync(tenantId, currentUser.UserId, tableKey, cancellationToken);
        await RecordAuditAsync(currentUser, "DELETE_DYNAMIC_TABLE", tableKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{tableKey}/relations")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> SetRelations(
        string tableKey,
        [FromBody] DynamicRelationUpsertRequest request,
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
        await _commandService.SetRelationsAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{tableKey}/field-permissions")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> SetFieldPermissions(
        string tableKey,
        [FromBody] DynamicFieldPermissionUpsertRequest request,
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
        await _commandService.SetFieldPermissionsAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 绑定/解绑审批流
    /// </summary>
    [HttpPut("{tableKey}/approval-binding")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> BindApprovalFlow(
        string tableKey,
        [FromBody] DynamicTableApprovalBindingRequest request,
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
        await _commandService.BindApprovalFlowAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        await RecordAuditAsync(currentUser, "BIND_DYNAMIC_TABLE_APPROVAL", tableKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{tableKey}/migrations")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<DynamicSchemaMigrationItem>>>> GetMigrations(
        string tableKey,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetMigrationHistoryAsync(tenantId, tableKey, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<DynamicSchemaMigrationItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{tableKey}/migrations/{migrationId:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.AppAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> RollbackMigration(
        string tableKey,
        long migrationId,
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
        await _commandService.RollbackMigrationAsync(tenantId, currentUser.UserId, tableKey, migrationId, cancellationToken);
        await RecordAuditAsync(currentUser, "ROLLBACK_DYNAMIC_TABLE_SCHEMA", tableKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey, MigrationId = migrationId.ToString() }, HttpContext.TraceIdentifier));
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
