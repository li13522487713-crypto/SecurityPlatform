using System.Diagnostics;
using Atlas.AppHost.Sdk.Hosting;
using Atlas.Core.Tenancy;
using Atlas.Core.Models;
using Atlas.Core.Setup;
using Atlas.Shared.Contracts.Health;
using Atlas.Shared.Contracts.Process;
using Atlas.Infrastructure.DataScopes.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// AppHost 健康探测控制器。
/// 提供 PlatformHost 进程管理所需的三个标准健康端点：
/// - /health/live   → 存活探测（始终返回 200）
/// - /health/ready  → 就绪探测（含数据库连通性检测）
/// - /health/info   → 详细信息（含 AppKey/TenantId/ReleaseVersion/Uptime）
/// </summary>
[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private static readonly Stopwatch UptimeStopwatch = Stopwatch.StartNew();

    private readonly IPlatformSqlSugarScopeFactory platformDbFactory;
    private readonly AppInstanceConfigurationLoader configLoader;
    private readonly ITenantProvider tenantProvider;
    private readonly ISetupStateProvider setupStateProvider;

    public HealthController(
        IPlatformSqlSugarScopeFactory platformDbFactory,
        AppInstanceConfigurationLoader configLoader,
        ITenantProvider tenantProvider,
        ISetupStateProvider setupStateProvider)
    {
        this.platformDbFactory = platformDbFactory;
        this.configLoader = configLoader;
        this.tenantProvider = tenantProvider;
        this.setupStateProvider = setupStateProvider;
    }

    /// <summary>
    /// 存活探测。
    /// 用于 kubelet/负载均衡判断进程是否存活，与任何依赖解耦。
    /// </summary>
    [HttpGet("live")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> Live()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            live = true,
            timestamp = DateTimeOffset.UtcNow.ToString("O")
        }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 就绪探测。
    /// 检测平台主库连通性和迁移状态，当 DB 不可达或存在 pending migration 时返回 false。
    /// </summary>
    [HttpGet("ready")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Ready(CancellationToken cancellationToken)
    {
        if (!setupStateProvider.IsReady)
        {
            var notReadyResponse = new
            {
                ready = false,
                reason = "setup_not_completed",
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            };
            return StatusCode(503, ApiResponse<object>.Fail(
                ErrorCodes.ServerError, "Platform setup not completed.", HttpContext.TraceIdentifier));
        }

        var dbHealthy = await CheckDatabaseConnectionAsync(cancellationToken);
        var migrationHealthy = await CheckMigrationStatusAsync(cancellationToken);
        var ready = dbHealthy && migrationHealthy;

        var response = new
        {
            ready,
            dbConnected = dbHealthy,
            migrationOk = migrationHealthy,
            timestamp = DateTimeOffset.UtcNow.ToString("O")
        };

        return ready
            ? Ok(ApiResponse<object>.Ok(response, HttpContext.TraceIdentifier))
            : StatusCode(503, ApiResponse<object>.Fail(ErrorCodes.ServerError, "AppHost not ready.", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 健康详情。
    /// 返回 AppHealthReport，包含 AppKey/InstanceId/TenantId/Uptime/DbConnected/MigrationStatus。
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AppHealthReport>>> Info(CancellationToken cancellationToken)
    {
        var config = configLoader.Load();
        var tenantId = tenantProvider.GetTenantId();
        var version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        if (!setupStateProvider.IsReady)
        {
            var report = new AppHealthReport
            {
                Status = AppProcessStatus.Unknown,
                Live = true,
                Ready = false,
                Version = version,
                Message = "Platform setup not completed.",
                CheckedAt = DateTimeOffset.UtcNow,
                AppKey = config.AppKey,
                InstanceId = config.InstanceId,
                TenantId = tenantId.ToString(),
                UptimeSeconds = UptimeStopwatch.Elapsed.TotalSeconds,
                DbConnected = false,
                MigrationStatus = "Unknown"
            };
            return Ok(ApiResponse<AppHealthReport>.Ok(report, HttpContext.TraceIdentifier));
        }

        var dbConnected = await CheckDatabaseConnectionAsync(cancellationToken);
        var migrationInfo = await GetMigrationStatusAsync(cancellationToken);

        var fullReport = new AppHealthReport
        {
            Status = dbConnected ? AppProcessStatus.Running : AppProcessStatus.Failed,
            Live = true,
            Ready = dbConnected,
            Version = version,
            Message = dbConnected ? "AppHost running." : "Database connection failed.",
            CheckedAt = DateTimeOffset.UtcNow,
            AppKey = config.AppKey,
            InstanceId = config.InstanceId,
            TenantId = tenantId.ToString(),
            UptimeSeconds = UptimeStopwatch.Elapsed.TotalSeconds,
            DbConnected = dbConnected,
            MigrationStatus = migrationInfo
        };

        return Ok(ApiResponse<AppHealthReport>.Ok(fullReport, HttpContext.TraceIdentifier));
    }

    private async Task<bool> CheckDatabaseConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var db = platformDbFactory.Create();
            _ = await db.Ado.SqlQueryAsync<int>(
                "SELECT 1",
                cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckMigrationStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var db = platformDbFactory.Create();
            var pendingCount = await db.Ado.SqlQueryAsync<int>(
                "SELECT COUNT(*) FROM SchemaMigrations WHERE Scope = 'App' AND ExecutedAt IS NULL",
                cancellationToken);
            return pendingCount.FirstOrDefault() == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetMigrationStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var db = platformDbFactory.Create();
            var result = await db.Ado.SqlQueryAsync<int>(
                "SELECT COUNT(*) FROM SchemaMigrations WHERE Scope = 'App' AND ExecutedAt IS NULL",
                cancellationToken);
            var pending = result.FirstOrDefault();
            return pending == 0 ? "UpToDate" : $"Pending:{pending}";
        }
        catch
        {
            return "Unknown";
        }
    }
}
