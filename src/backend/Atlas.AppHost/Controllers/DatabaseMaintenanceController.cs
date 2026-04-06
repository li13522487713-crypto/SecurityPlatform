using Atlas.Application.Setup;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 应用宿主数据库运维控制器（精简版：只读操作 + 备份）。
/// </summary>
[ApiController]
[Route("api/v1/database-maintenance")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class DatabaseMaintenanceController : ControllerBase
{
    private readonly IDatabaseMaintenanceService _maintenanceService;

    public DatabaseMaintenanceController(IDatabaseMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpGet("capabilities")]
    public async Task<ActionResult<ApiResponse<DatabaseMaintenanceCapability>>> GetCapabilities(CancellationToken ct)
    {
        var capability = await _maintenanceService.GetCapabilityAsync(ct);
        return Ok(ApiResponse<DatabaseMaintenanceCapability>.Ok(capability, HttpContext.TraceIdentifier));
    }

    [HttpGet("test-connection")]
    public async Task<ActionResult<ApiResponse<DatabaseConnectionStatus>>> TestConnection(CancellationToken ct)
    {
        var result = await _maintenanceService.TestConnectionAsync(ct);
        return Ok(ApiResponse<DatabaseConnectionStatus>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("info")]
    public async Task<ActionResult<ApiResponse<DatabaseInfo>>> GetInfo(CancellationToken ct)
    {
        var info = await _maintenanceService.GetDatabaseInfoAsync(ct);
        return Ok(ApiResponse<DatabaseInfo>.Ok(info, HttpContext.TraceIdentifier));
    }

    [HttpGet("backups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BackupFileInfo>>>> ListBackups(CancellationToken ct)
    {
        var backups = await _maintenanceService.ListBackupsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<BackupFileInfo>>.Ok(backups, HttpContext.TraceIdentifier));
    }

    [HttpPost("backups")]
    public async Task<ActionResult<ApiResponse<BackupResult>>> BackupNow(CancellationToken ct)
    {
        var result = await _maintenanceService.BackupNowAsync(ct);
        return Ok(ApiResponse<BackupResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
