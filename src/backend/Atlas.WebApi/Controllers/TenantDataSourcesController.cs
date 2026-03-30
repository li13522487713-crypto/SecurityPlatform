using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 租户数据源管理（等保2.0 数据隔离）
/// </summary>
[ApiController]
[Route("api/v1/tenant-datasources")]
[Authorize]
public sealed class TenantDataSourcesController : ControllerBase
{
    private readonly ITenantDataSourceService _tenantDataSourceService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Application.System.Abstractions.ISqlQueryService _sqlQueryService;

    public TenantDataSourcesController(
        ITenantDataSourceService tenantDataSourceService,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        ITenantProvider tenantProvider,
        Atlas.Application.System.Abstractions.ISqlQueryService sqlQueryService)
    {
        _tenantDataSourceService = tenantDataSourceService;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _tenantProvider = tenantProvider;
        _sqlQueryService = sqlQueryService;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantDataSourceDto>>>> GetAll(CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<IReadOnlyList<TenantDataSourceDto>>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var dtos = await _tenantDataSourceService.QueryAllAsync(tenantIdValue, ct);
        return Ok(ApiResponse<IReadOnlyList<TenantDataSourceDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    [HttpGet("drivers")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DataSourceDriverDefinition>>>> GetDrivers(CancellationToken ct = default)
    {
        var result = await _tenantDataSourceService.GetDriverDefinitionsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<DataSourceDriverDefinition>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.DataSourcesCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] TenantDataSourceCreateRequest request,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"),
                HttpContext.TraceIdentifier));
        }
        if (!string.IsNullOrWhiteSpace(request.TenantIdValue)
            && !string.Equals(request.TenantIdValue, tenantIdValue, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                ErrorCodes.CrossTenantForbidden,
                ApiResponseLocalizer.T(HttpContext, "TenantContextMismatch"),
                HttpContext.TraceIdentifier));
        }

        var normalizedRequest = request with { TenantIdValue = tenantIdValue };
        var id = await _tenantDataSourceService.CreateAsync(tenantIdValue, normalizedRequest, ct);

        await RecordAuditAsync("DATASOURCE_CREATE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] TenantDataSourceUpdateRequest request,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var updated = await _tenantDataSourceService.UpdateAsync(tenantIdValue, id, request, ct);
        if (!updated)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "TenantDataSourceNotFound"), HttpContext.TraceIdentifier));
        }

        await RecordAuditAsync("DATASOURCE_UPDATE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var deleted = await _tenantDataSourceService.DeleteAsync(tenantIdValue, id, ct);
        if (!deleted)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "TenantDataSourceNotFound"), HttpContext.TraceIdentifier));
        }

        await RecordAuditAsync("DATASOURCE_DELETE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("test")]
    [Authorize(Policy = PermissionPolicies.DataSourcesUpdate)]
    public async Task<ActionResult<ApiResponse<TestConnectionResult>>> TestConnection(
        [FromBody] TestConnectionRequest request,
        CancellationToken ct = default)
    {
        var result = await _tenantDataSourceService.TestConnectionAsync(request, ct);
        await RecordAuditAsync("DATASOURCE_TEST", "ad-hoc", ct);
        return Ok(ApiResponse<TestConnectionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/test")]
    [Authorize(Policy = PermissionPolicies.DataSourcesUpdate)]
    public async Task<ActionResult<ApiResponse<TestConnectionResult>>> TestConnectionById(
        long id,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<TestConnectionResult>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var result = await _tenantDataSourceService.TestConnectionByDataSourceIdAsync(tenantIdValue, id, ct);
        if (!result.Success && string.Equals(result.ErrorMessage, "TenantDataSourceNotFound", StringComparison.Ordinal))
        {
            return NotFound(ApiResponse<TestConnectionResult>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "TenantDataSourceNotFound"),
                HttpContext.TraceIdentifier));
        }

        await RecordAuditAsync("DATASOURCE_TEST", id.ToString(), ct);
        return Ok(ApiResponse<TestConnectionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/query")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<Atlas.Application.System.Models.SqlQueryResult>>> Query(
        long id,
        [FromBody] Atlas.Application.System.Models.SqlQueryRequest request,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<Atlas.Application.System.Models.SqlQueryResult>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var result = await _sqlQueryService.ExecutePreviewQueryAsync(tenantIdValue, id, request, ct);
        if (!result.Success && result.ErrorMessage == "Data source not found.")
        {
            return NotFound(ApiResponse<Atlas.Application.System.Models.SqlQueryResult>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "TenantDataSourceNotFound"),
                HttpContext.TraceIdentifier));
        }
        
        await RecordAuditAsync("DATASOURCE_QUERY", id.ToString(), ct);
        return Ok(ApiResponse<Atlas.Application.System.Models.SqlQueryResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/schema")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<DataSourceSchemaResult>>> GetSchema(
        long id,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<DataSourceSchemaResult>.Fail(
                ErrorCodes.ValidationError,
                ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"),
                HttpContext.TraceIdentifier));
        }

        var result = await _sqlQueryService.GetSchemaAsync(tenantIdValue, id, ct);
        if (!result.Success && result.ErrorMessage == "Data source not found.")
        {
            return NotFound(ApiResponse<DataSourceSchemaResult>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "TenantDataSourceNotFound"),
                HttpContext.TraceIdentifier));
        }

        await RecordAuditAsync("DATASOURCE_SCHEMA", id.ToString(), ct);
        return Ok(ApiResponse<DataSourceSchemaResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/consumers")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DataSourceConsumerItem>>>> GetConsumers(
        long id,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<IReadOnlyList<DataSourceConsumerItem>>.Fail(
                ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"), HttpContext.TraceIdentifier));
        }

        var result = await _tenantDataSourceService.GetConsumersAsync(tenantIdValue, id, ct);
        return Ok(ApiResponse<IReadOnlyList<DataSourceConsumerItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("orphans")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DataSourceOrphanItem>>>> GetOrphans(
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<IReadOnlyList<DataSourceOrphanItem>>.Fail(
                ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "TenantIdRequired"), HttpContext.TraceIdentifier));
        }

        var result = await _tenantDataSourceService.GetOrphansAsync(tenantIdValue, ct);
        return Ok(ApiResponse<IReadOnlyList<DataSourceOrphanItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    private string ResolveTenantIdValue()
    {
        var tenantId = _tenantProvider.GetTenantId();
        return tenantId.IsEmpty ? string.Empty : tenantId.Value.ToString();
    }

    private async Task RecordAuditAsync(string action, string target, CancellationToken ct)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return;
        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;
        var auditContext = new AuditContext(
            currentUser.TenantId, actor, action, "SUCCESS", target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, ct);
    }
}
