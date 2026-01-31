using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
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
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<DynamicTableCreateRequest> _createValidator;
    private readonly IValidator<DynamicTableUpdateRequest> _updateValidator;

    public DynamicTablesController(
        IDynamicTableQueryService queryService,
        IDynamicTableCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<DynamicTableCreateRequest> createValidator,
        IValidator<DynamicTableUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
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
        var result = await _queryService.QueryAsync(request, tenantId, cancellationToken);
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
                "TableKey不能为空",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByKeyAsync(tenantId, tableKey, cancellationToken);
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
                "TableKey不能为空",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var fields = await _queryService.GetFieldsAsync(tenantId, tableKey, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DynamicFieldDefinition>>.Ok(fields, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
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
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{tableKey}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
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
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{tableKey}/schema/alter")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
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
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.AlterAsync(tenantId, currentUser.UserId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{tableKey}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string tableKey,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, currentUser.UserId, tableKey, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { TableKey = tableKey }, HttpContext.TraceIdentifier));
    }
}
