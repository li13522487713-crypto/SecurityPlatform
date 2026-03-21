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
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class TenantDataSourcesController : ControllerBase
{
    private readonly ITenantDataSourceService _tenantDataSourceService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;
    private readonly ITenantProvider _tenantProvider;

    public TenantDataSourcesController(
        ITenantDataSourceService tenantDataSourceService,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder,
        ITenantProvider tenantProvider)
    {
        _tenantDataSourceService = tenantDataSourceService;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantDataSourceDto>>>> GetAll(CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<IReadOnlyList<TenantDataSourceDto>>.Fail(
                ErrorCodes.ValidationError,
                "缺少租户标识",
                HttpContext.TraceIdentifier));
        }

        var dtos = await _tenantDataSourceService.QueryAllAsync(tenantIdValue, ct);
        return Ok(ApiResponse<IReadOnlyList<TenantDataSourceDto>>.Ok(dtos, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] TenantDataSourceCreateRequest request,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "缺少租户标识",
                HttpContext.TraceIdentifier));
        }
        if (!string.IsNullOrWhiteSpace(request.TenantIdValue)
            && !string.Equals(request.TenantIdValue, tenantIdValue, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                ErrorCodes.CrossTenantForbidden,
                "租户上下文不一致",
                HttpContext.TraceIdentifier));
        }

        var normalizedRequest = request with { TenantIdValue = tenantIdValue };
        var id = await _tenantDataSourceService.CreateAsync(tenantIdValue, normalizedRequest, ct);

        await RecordAuditAsync("DATASOURCE_CREATE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
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
                "缺少租户标识",
                HttpContext.TraceIdentifier));
        }

        var updated = await _tenantDataSourceService.UpdateAsync(tenantIdValue, id, request, ct);
        if (!updated)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "数据源不存在", HttpContext.TraceIdentifier));
        }

        await RecordAuditAsync("DATASOURCE_UPDATE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "缺少租户标识",
                HttpContext.TraceIdentifier));
        }

        var deleted = await _tenantDataSourceService.DeleteAsync(tenantIdValue, id, ct);
        if (!deleted)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "数据源不存在", HttpContext.TraceIdentifier));
        }

        await RecordAuditAsync("DATASOURCE_DELETE", id.ToString(), ct);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("test")]
    public async Task<ActionResult<ApiResponse<TestConnectionResult>>> TestConnection(
        [FromBody] TestConnectionRequest request,
        CancellationToken ct = default)
    {
        var result = await _tenantDataSourceService.TestConnectionAsync(request, ct);
        await RecordAuditAsync("DATASOURCE_TEST", "ad-hoc", ct);
        return Ok(ApiResponse<TestConnectionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/test")]
    public async Task<ActionResult<ApiResponse<TestConnectionResult>>> TestConnectionById(
        long id,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<TestConnectionResult>.Fail(
                ErrorCodes.ValidationError,
                "缺少租户标识",
                HttpContext.TraceIdentifier));
        }

        var result = await _tenantDataSourceService.TestConnectionByDataSourceIdAsync(tenantIdValue, id, ct);
        if (!result.Success && string.Equals(result.ErrorMessage, "数据源不存在", StringComparison.Ordinal))
        {
            return NotFound(ApiResponse<TestConnectionResult>.Fail(
                ErrorCodes.NotFound,
                "数据源不存在",
                HttpContext.TraceIdentifier));
        }

        await RecordAuditAsync("DATASOURCE_TEST", id.ToString(), ct);
        return Ok(ApiResponse<TestConnectionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/consumers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DataSourceConsumerItem>>>> GetConsumers(
        long id,
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<IReadOnlyList<DataSourceConsumerItem>>.Fail(
                ErrorCodes.ValidationError, "缺少租户标识", HttpContext.TraceIdentifier));
        }

        var result = await _tenantDataSourceService.GetConsumersAsync(tenantIdValue, id, ct);
        return Ok(ApiResponse<IReadOnlyList<DataSourceConsumerItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("orphans")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DataSourceOrphanItem>>>> GetOrphans(
        CancellationToken ct = default)
    {
        var tenantIdValue = ResolveTenantIdValue();
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return BadRequest(ApiResponse<IReadOnlyList<DataSourceOrphanItem>>.Fail(
                ErrorCodes.ValidationError, "缺少租户标识", HttpContext.TraceIdentifier));
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
