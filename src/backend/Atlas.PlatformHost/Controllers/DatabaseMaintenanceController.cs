using Atlas.Application.Setup;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 数据库运维控制器（备份、恢复、连接检测、数据库信息）。
/// 需要管理员权限。
/// </summary>
[ApiController]
[Route("api/v1/database-maintenance")]
[Authorize]
public sealed class DatabaseMaintenanceController : ControllerBase
{
    private readonly IDatabaseMaintenanceService _maintenanceService;

    public DatabaseMaintenanceController(IDatabaseMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    /// <summary>测试当前数据库连接</summary>
    [HttpGet("test-connection")]
    public async Task<ActionResult<ApiResponse<DatabaseConnectionStatus>>> TestConnection(CancellationToken cancellationToken)
    {
        var result = await _maintenanceService.TestConnectionAsync(cancellationToken);
        return Ok(ApiResponse<DatabaseConnectionStatus>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>获取数据库信息</summary>
    [HttpGet("info")]
    public async Task<ActionResult<ApiResponse<DatabaseInfo>>> GetInfo(CancellationToken cancellationToken)
    {
        var info = await _maintenanceService.GetDatabaseInfoAsync(cancellationToken);
        return Ok(ApiResponse<DatabaseInfo>.Ok(info, HttpContext.TraceIdentifier));
    }

    /// <summary>获取备份文件列表</summary>
    [HttpGet("backups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BackupFileInfo>>>> ListBackups(CancellationToken cancellationToken)
    {
        var backups = await _maintenanceService.ListBackupsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BackupFileInfo>>.Ok(backups, HttpContext.TraceIdentifier));
    }

    /// <summary>立即执行手动备份</summary>
    [HttpPost("backups")]
    public async Task<ActionResult<ApiResponse<BackupResult>>> BackupNow(CancellationToken cancellationToken)
    {
        var result = await _maintenanceService.BackupNowAsync(cancellationToken);
        return Ok(ApiResponse<BackupResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>从备份文件恢复</summary>
    [HttpPost("restore")]
    public async Task<ActionResult<ApiResponse<object>>> Restore(
        [FromBody] RestoreRequest request,
        CancellationToken cancellationToken)
    {
        await _maintenanceService.RestoreFromBackupAsync(request.BackupFileName, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

public sealed record RestoreRequest(string BackupFileName);
