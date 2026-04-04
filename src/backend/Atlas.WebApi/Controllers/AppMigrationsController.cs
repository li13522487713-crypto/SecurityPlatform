using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/app-migrations")]
[Authorize]
[PlatformOnly]
public sealed class AppMigrationsController : ControllerBase
{
    private readonly IAppMigrationService _migrationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ITenantProvider _tenantProvider;

    public AppMigrationsController(
        IAppMigrationService migrationService,
        ICurrentUserAccessor currentUserAccessor,
        ITenantProvider tenantProvider)
    {
        _migrationService = migrationService;
        _currentUserAccessor = currentUserAccessor;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppMigrationTaskListItem>>>> Query(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _migrationService.QueryTasksAsync(
            tenantId,
            new PagedRequest(pageIndex, pageSize, keyword),
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AppMigrationTaskListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AppMigrationTaskCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var userId = user?.UserId ?? 0;
        var id = await _migrationService.CreateTaskAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("repair-primary-binding")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppMigrationBindingRepairResult>>> RepairPrimaryBinding(
        [FromBody] AppMigrationBindingRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var userId = user?.UserId ?? 0;
        var result = await _migrationService.RepairPrimaryBindingAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<AppMigrationBindingRepairResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<AppMigrationTaskDetail?>>> Get(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _migrationService.GetTaskAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AppMigrationTaskDetail?>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/precheck")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppMigrationPrecheckResult>>> Precheck(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _migrationService.PrecheckAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AppMigrationPrecheckResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/start")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppMigrationActionResult>>> Start(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var result = await _migrationService.StartAsync(tenantId, user?.UserId ?? 0, id, cancellationToken);
        return Ok(ApiResponse<AppMigrationActionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/progress")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<AppMigrationTaskProgress?>>> Progress(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var progress = await _migrationService.GetProgressAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AppMigrationTaskProgress?>.Ok(progress, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/validate")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppIntegrityCheckSummary>>> Validate(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var result = await _migrationService.ValidateIntegrityAsync(tenantId, user?.UserId ?? 0, id, cancellationToken);
        return Ok(ApiResponse<AppIntegrityCheckSummary>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/cutover")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppMigrationActionResult>>> Cutover(
        long id,
        [FromBody] AppMigrationCutoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var result = await _migrationService.CutoverAsync(tenantId, user?.UserId ?? 0, id, request, cancellationToken);
        return Ok(ApiResponse<AppMigrationActionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppMigrationActionResult>>> Rollback(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var result = await _migrationService.RollbackCutoverAsync(tenantId, user?.UserId ?? 0, id, cancellationToken);
        return Ok(ApiResponse<AppMigrationActionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/reset")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    public async Task<ActionResult<ApiResponse<AppMigrationActionResult>>> Reset(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var result = await _migrationService.ResetFailedTaskAsync(tenantId, user?.UserId ?? 0, id, cancellationToken);
        return Ok(ApiResponse<AppMigrationActionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/recover")]
    [Authorize(Policy = PermissionPolicies.AppsUpdate)]
    [SkipIdempotency]
    public async Task<ActionResult<ApiResponse<AppMigrationActionResult>>> Recover(
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUserAccessor.GetCurrentUser();
        var result = await _migrationService.RecoverCorruptedTaskAsync(tenantId, user?.UserId ?? 0, id, cancellationToken);
        return Ok(ApiResponse<AppMigrationActionResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
