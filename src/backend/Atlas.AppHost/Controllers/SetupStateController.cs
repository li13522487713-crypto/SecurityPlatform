using Atlas.Core.Models;
using Atlas.Core.Setup;
using Atlas.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 应用宿主安装状态与初始化端点。
/// 暴露平台和应用两级 setup 状态，并提供应用级最小安装向导。
/// </summary>
[ApiController]
[Route("api/v1/setup")]
[AllowAnonymous]
public sealed class SetupStateController : ControllerBase
{
    private readonly ISetupStateProvider _platformSetupStateProvider;
    private readonly IAppSetupStateProvider _appSetupStateProvider;
    private readonly ISetupDbClientFactory _setupDbClientFactory;
    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<SetupStateController> _logger;

    public SetupStateController(
        ISetupStateProvider platformSetupStateProvider,
        IAppSetupStateProvider appSetupStateProvider,
        ISetupDbClientFactory setupDbClientFactory,
        IOptions<DatabaseOptions> databaseOptions,
        ILogger<SetupStateController> logger)
    {
        _platformSetupStateProvider = platformSetupStateProvider;
        _appSetupStateProvider = appSetupStateProvider;
        _setupDbClientFactory = setupDbClientFactory;
        _databaseOptions = databaseOptions.Value;
        _logger = logger;
    }

    /// <summary>获取平台和应用两级 setup 状态</summary>
    [HttpGet("state")]
    public ActionResult<ApiResponse<AppHostSetupStateResponse>> GetState()
    {
        var platformState = _platformSetupStateProvider.GetState();
        var appState = _appSetupStateProvider.GetState();
        return Ok(ApiResponse<AppHostSetupStateResponse>.Ok(
            new AppHostSetupStateResponse(
                platformState.Status.ToString(),
                platformState.PlatformSetupCompleted,
                appState.Status.ToString(),
                _appSetupStateProvider.IsReady),
            HttpContext.TraceIdentifier));
    }

    /// <summary>执行应用级初始化：验证数据库连接、确认平台已初始化、记录应用信息</summary>
    [HttpPost("initialize")]
    public async Task<ActionResult<ApiResponse<AppSetupInitializeResponse>>> Initialize(
        [FromBody] AppSetupInitializeRequest request,
        CancellationToken cancellationToken)
    {
        if (!_platformSetupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<AppSetupInitializeResponse>.Fail(
                "PLATFORM_NOT_READY", "Platform setup must be completed first.", HttpContext.TraceIdentifier));
        }

        if (_appSetupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<AppSetupInitializeResponse>.Fail(
                "ALREADY_CONFIGURED", "Application setup has already been completed.", HttpContext.TraceIdentifier));
        }

        try
        {
            await _appSetupStateProvider.TransitionAsync(AppSetupState.Initializing, cancellationToken: cancellationToken);

            var dbConnected = false;
            var coreTablesVerified = false;
            var verificationErrors = new List<string>();

            // 1. 验证数据库连接可用（使用 setup 工厂绕过 ISqlSugarClient 门禁）
            try
            {
                using var db = _setupDbClientFactory.Create(
                    _databaseOptions.ConnectionString,
                    _databaseOptions.DbType);
                await db.Ado.GetScalarAsync("SELECT 1", cancellationToken);
                dbConnected = true;
                _logger.LogInformation("[AppSetup] 数据库连接验证成功");
            }
            catch (Exception dbEx)
            {
                verificationErrors.Add($"Database connection failed: {dbEx.Message}");
                _logger.LogError(dbEx, "[AppSetup] 数据库连接验证失败");
            }

            // 2. 验证平台核心表已初始化（检查 sys_user 表存在）
            if (dbConnected)
            {
                try
                {
                    using var db = _setupDbClientFactory.Create(
                        _databaseOptions.ConnectionString,
                        _databaseOptions.DbType);
                    var tables = db.DbMaintenance.GetTableInfoList();
                    var tableNames = tables.Select(t => t.Name.ToLowerInvariant()).ToHashSet();
                    coreTablesVerified = tableNames.Contains("sys_user") && tableNames.Contains("sys_role");

                    if (!coreTablesVerified)
                    {
                        verificationErrors.Add("Core tables (sys_user, sys_role) not found. Platform initialization may be incomplete.");
                    }
                    else
                    {
                        _logger.LogInformation("[AppSetup] 平台核心表验证通过，共 {Count} 张表", tables.Count);
                    }
                }
                catch (Exception tableEx)
                {
                    verificationErrors.Add($"Table verification failed: {tableEx.Message}");
                    _logger.LogError(tableEx, "[AppSetup] 平台核心表验证失败");
                }
            }

            if (verificationErrors.Count > 0)
            {
                await _appSetupStateProvider.TransitionAsync(
                    AppSetupState.Failed,
                    string.Join("; ", verificationErrors),
                    cancellationToken);
                return StatusCode(500, ApiResponse<AppSetupInitializeResponse>.Fail(
                    "APP_SETUP_FAILED",
                    string.Join("; ", verificationErrors),
                    HttpContext.TraceIdentifier));
            }

            // 3. 记录应用信息并标记完成
            await _appSetupStateProvider.CompleteSetupAsync(
                request.AppName,
                request.AdminUsername,
                cancellationToken);

            _logger.LogInformation("[AppSetup] 应用安装完成，AppName={AppName}", request.AppName);

            var platformState = _platformSetupStateProvider.GetState();
            var appState = _appSetupStateProvider.GetState();
            return Ok(ApiResponse<AppSetupInitializeResponse>.Ok(
                new AppSetupInitializeResponse(
                    platformState.Status.ToString(),
                    platformState.PlatformSetupCompleted,
                    appState.Status.ToString(),
                    true,
                    dbConnected,
                    coreTablesVerified,
                    verificationErrors),
                HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AppSetup] 应用初始化失败");
            await _appSetupStateProvider.TransitionAsync(AppSetupState.Failed, ex.Message, cancellationToken);
            return StatusCode(500, ApiResponse<AppSetupInitializeResponse>.Fail(
                "APP_SETUP_FAILED", ex.Message, HttpContext.TraceIdentifier));
        }
    }
}

public sealed record AppHostSetupStateResponse(
    string PlatformStatus,
    bool PlatformSetupCompleted,
    string AppStatus,
    bool AppSetupCompleted);

public sealed record AppSetupInitializeResponse(
    string PlatformStatus,
    bool PlatformSetupCompleted,
    string AppStatus,
    bool AppSetupCompleted,
    bool DatabaseConnected,
    bool CoreTablesVerified,
    List<string> Errors);

public sealed record AppSetupInitializeRequest(
    string AppName,
    string AdminUsername);
