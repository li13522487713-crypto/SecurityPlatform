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
[Route("api/v1/dynamic-migrations")]
public sealed class DynamicMigrationsController : ControllerBase
{
    private readonly IMigrationService _migrationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<MigrationRecordCreateRequest> _createValidator;

    public DynamicMigrationsController(
        IMigrationService migrationService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<MigrationRecordCreateRequest> createValidator)
    {
        _migrationService = migrationService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<MigrationRecordListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] string? tableKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _migrationService.QueryAsync(request, tenantId, tableKey, cancellationToken);
        return Ok(ApiResponse<PagedResult<MigrationRecordListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<MigrationRecordDetail?>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _migrationService.GetByIdAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<MigrationRecordDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] MigrationRecordCreateRequest request,
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
        var id = await _migrationService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("detect/{tableKey}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<MigrationScriptPreview>>> DetectChanges(
        string tableKey,
        [FromBody] DynamicTableAlterRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var preview = await _migrationService.DetectChangesAsync(tenantId, tableKey, request, cancellationToken);
        return Ok(ApiResponse<MigrationScriptPreview>.Ok(preview, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/execute")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<MigrationExecutionResult>>> Execute(
        long id,
        [FromBody] MigrationExecuteRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<MigrationExecutionResult>.Fail(
                ErrorCodes.Unauthorized,
                "未登录",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var confirmDestructive = request?.ConfirmDestructive ?? false;
        var result = await _migrationService.ExecuteAsync(tenantId, currentUser.UserId, id, confirmDestructive, cancellationToken);
        return Ok(ApiResponse<MigrationExecutionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/precheck")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<MigrationPrecheckResult>>> Precheck(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _migrationService.PrecheckAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<MigrationPrecheckResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/retry")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public Task<ActionResult<ApiResponse<MigrationExecutionResult>>> Retry(
        long id,
        [FromBody] MigrationExecuteRequest? request,
        CancellationToken cancellationToken)
    {
        return Execute(id, request, cancellationToken);
    }
}
