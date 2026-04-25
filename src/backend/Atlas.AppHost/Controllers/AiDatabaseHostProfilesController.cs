using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Services;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/ai-database-host-profiles")]
[Authorize]
public sealed class AiDatabaseHostProfilesController : ControllerBase
{
    private readonly IAiDatabaseHostProfileService _service;
    private readonly IAiDatabaseSecretProtector _secretProtector;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAuditWriter _auditWriter;

    public AiDatabaseHostProfilesController(
        IAiDatabaseHostProfileService service,
        IAiDatabaseSecretProtector secretProtector,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IAuditWriter auditWriter)
    {
        _service = service;
        _secretProtector = secretProtector;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _auditWriter = auditWriter;
    }

    [HttpGet("drivers")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiDatabaseDriverDto>>>> GetDrivers(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<AiDatabaseDriverDto>>.Ok(await _service.GetAvailableDriversAsync(cancellationToken), HttpContext.TraceIdentifier));

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AiDatabaseHostProfileDto>>>> List(CancellationToken cancellationToken)
    {
        var result = await _service.ListProfilesAsync(_tenantProvider.GetTenantId(), cancellationToken);
        return Ok(ApiResponse<PagedResult<AiDatabaseHostProfileDto>>.Ok(new PagedResult<AiDatabaseHostProfileDto>(result, result.Count, 1, result.Count == 0 ? 20 : result.Count), HttpContext.TraceIdentifier));
    }

    [HttpGet("{profileId}")]
    [Authorize(Policy = PermissionPolicies.DataSourcesView)]
    public async Task<ActionResult<ApiResponse<AiDatabaseHostProfileDto>>> Get(string profileId, CancellationToken cancellationToken)
    {
        var result = await _service.GetProfileAsync(_tenantProvider.GetTenantId(), profileId, cancellationToken);
        return result is null
            ? NotFound(ApiResponse<AiDatabaseHostProfileDto>.Fail("NOT_FOUND", "托管配置不存在。", HttpContext.TraceIdentifier))
            : Ok(ApiResponse<AiDatabaseHostProfileDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiDatabaseHostProfileManage)]
    public async Task<ActionResult<ApiResponse<AiDatabaseHostProfileDto>>> Create(
        [FromBody] AiDatabaseHostProfileCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateProfileAsync(_tenantProvider.GetTenantId(), request, CurrentUserId(), cancellationToken);
        await WriteAuditAsync("ai_database_host_profile.create", result.Id, cancellationToken);
        return Ok(ApiResponse<AiDatabaseHostProfileDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{profileId}")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseHostProfileManage)]
    public async Task<ActionResult<ApiResponse<AiDatabaseHostProfileDto>>> Update(
        string profileId,
        [FromBody] AiDatabaseHostProfileUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateProfileAsync(_tenantProvider.GetTenantId(), profileId, request, CurrentUserId(), cancellationToken);
        await WriteAuditAsync("ai_database_host_profile.update", profileId, cancellationToken);
        return Ok(ApiResponse<AiDatabaseHostProfileDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{profileId}/test")]
    [Authorize(Policy = PermissionPolicies.DataSourcesQuery)]
    public async Task<ActionResult<ApiResponse<AiDatabaseConnectionTestResult>>> Test(string profileId, CancellationToken cancellationToken)
    {
        var result = await _service.TestProfileConnectionAsync(_tenantProvider.GetTenantId(), profileId, cancellationToken);
        await WriteAuditAsync("ai_database_host_profile.test", profileId, cancellationToken);
        return Ok(ApiResponse<AiDatabaseConnectionTestResult>.Ok(result with { TraceId = HttpContext.TraceIdentifier }, HttpContext.TraceIdentifier));
    }

    [HttpPost("test")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseHostProfileManage)]
    public async Task<ActionResult<ApiResponse<AiDatabaseConnectionTestResult>>> TestUnsaved(
        [FromBody] AiDatabaseHostProfileProbeRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            return await Test(request.ProfileId, cancellationToken);
        }

        if (string.Equals(request.DriverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(ApiResponse<AiDatabaseConnectionTestResult>.Ok(new AiDatabaseConnectionTestResult(true, "SQLite configuration accepted.", DateTime.UtcNow, HttpContext.TraceIdentifier), HttpContext.TraceIdentifier));
        }

        try
        {
            using var client = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = request.ConnectionString ?? string.Empty,
                DbType = DataSourceDriverRegistry.ResolveDbType(request.DriverCode),
                IsAutoCloseConnection = true
            });
            await client.Ado.GetScalarAsync("SELECT 1");
            return Ok(ApiResponse<AiDatabaseConnectionTestResult>.Ok(new AiDatabaseConnectionTestResult(true, "Connection succeeded.", DateTime.UtcNow, HttpContext.TraceIdentifier), HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<AiDatabaseConnectionTestResult>.Ok(new AiDatabaseConnectionTestResult(false, _secretProtector.MaskConnectionString(ex.Message), DateTime.UtcNow, HttpContext.TraceIdentifier), HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("{profileId}/set-default")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseHostProfileManage)]
    public async Task<ActionResult<ApiResponse<object>>> SetDefault(string profileId, CancellationToken cancellationToken)
    {
        await _service.SetDefaultProfileAsync(_tenantProvider.GetTenantId(), profileId, cancellationToken);
        await WriteAuditAsync("ai_database_host_profile.set_default", profileId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { profileId }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{profileId}")]
    [Authorize(Policy = PermissionPolicies.AiDatabaseHostProfileManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string profileId, CancellationToken cancellationToken)
    {
        await _service.DeleteProfileAsync(_tenantProvider.GetTenantId(), profileId, cancellationToken);
        await WriteAuditAsync("ai_database_host_profile.delete", profileId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { profileId }, HttpContext.TraceIdentifier));
    }

    private string CurrentUserId()
        => _currentUserAccessor.GetCurrentUserOrThrow().UserId.ToString();

    private async Task WriteAuditAsync(string action, string targetId, CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.GetCurrentUserOrThrow();
        await _auditWriter.WriteAsync(
            new AuditRecord(
                _tenantProvider.GetTenantId(),
                user.UserId.ToString(),
                action,
                "success",
                $"ai-database-host-profile:{targetId}",
                null,
                null),
            cancellationToken);
    }
}

public sealed record AiDatabaseHostProfileProbeRequest(
    string DriverCode,
    string? ConnectionString = null,
    string? ProfileId = null);
