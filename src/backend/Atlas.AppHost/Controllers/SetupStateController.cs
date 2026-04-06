using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Setup;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using System.Text.Json;

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
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SetupStateController> _logger;

    public SetupStateController(
        ISetupStateProvider platformSetupStateProvider,
        IAppSetupStateProvider appSetupStateProvider,
        ISetupDbClientFactory setupDbClientFactory,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<SetupStateController> logger)
    {
        _platformSetupStateProvider = platformSetupStateProvider;
        _appSetupStateProvider = appSetupStateProvider;
        _setupDbClientFactory = setupDbClientFactory;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>获取平台和应用两级 setup 状态</summary>
    [HttpGet("state")]
    public ActionResult<ApiResponse<AppHostSetupStateResponse>> GetState()
    {
        var platformState = ResolvePlatformState();
        var appState = _appSetupStateProvider.GetState();
        return Ok(ApiResponse<AppHostSetupStateResponse>.Ok(
            new AppHostSetupStateResponse(
                platformState.Status.ToString(),
                platformState.PlatformSetupCompleted,
                appState.Status.ToString(),
                _appSetupStateProvider.IsReady),
            HttpContext.TraceIdentifier));
    }

    /// <summary>获取可用的数据库驱动列表</summary>
    [HttpGet("drivers")]
    public ActionResult<ApiResponse<IReadOnlyList<DataSourceDriverDefinition>>> GetDrivers()
    {
        var drivers = DataSourceDriverRegistry.GetDefinitions();
        return Ok(ApiResponse<IReadOnlyList<DataSourceDriverDefinition>>.Ok(drivers, HttpContext.TraceIdentifier));
    }

    /// <summary>测试应用数据库连接</summary>
    [HttpPost("test-connection")]
    public async Task<ActionResult<ApiResponse<SetupTestConnectionResponse>>> TestConnection(
        [FromBody] AppSetupTestConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (_appSetupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<SetupTestConnectionResponse>.Fail(
                "ALREADY_CONFIGURED", "Application setup has already been completed.", HttpContext.TraceIdentifier));
        }

        try
        {
            var connectionString = ResolveConnectionString(request.Database);
            var dbType = DataSourceDriverRegistry.ResolveDbType(request.Database.DriverCode);
            var resolvedConnectionString = ResolveRuntimeConnectionString(connectionString, request.Database.DriverCode);

            if (dbType == DbType.Sqlite)
            {
                await TestSqliteConnectionAsync(resolvedConnectionString, cancellationToken);
            }
            else
            {
                await TestGenericConnectionAsync(resolvedConnectionString, dbType, cancellationToken);
            }

            return Ok(ApiResponse<SetupTestConnectionResponse>.Ok(
                new SetupTestConnectionResponse(true, "Connection successful."),
                HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AppSetup] 数据库连接测试失败");
            return Ok(ApiResponse<SetupTestConnectionResponse>.Ok(
                new SetupTestConnectionResponse(false, ex.Message),
                HttpContext.TraceIdentifier));
        }
    }

    private SetupStateInfo ResolvePlatformState()
    {
        var state = _platformSetupStateProvider.GetState();
        if (state.Status != SetupState.NotConfigured)
        {
            return state;
        }

        var setupStatePath = _configuration["Setup:StateFilePath"];
        if (string.IsNullOrWhiteSpace(setupStatePath) || !System.IO.File.Exists(setupStatePath))
        {
            return state;
        }

        try
        {
            var json = System.IO.File.ReadAllText(setupStatePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return state;
            }

            return JsonSerializer.Deserialize<SetupStateInfo>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AppSetup] 读取平台 setup-state.json 直连回退失败");
            return state;
        }
    }

    /// <summary>执行应用级初始化：验证数据库连接、确认平台已初始化、记录应用信息</summary>
    [HttpPost("initialize")]
    public async Task<ActionResult<ApiResponse<AppSetupInitializeResponse>>> Initialize(
        [FromBody] AppSetupInitializeRequest request,
        CancellationToken cancellationToken)
    {
        var platformState = ResolvePlatformState();
        if (platformState.Status != SetupState.Ready)
        {
            return BadRequest(ApiResponse<AppSetupInitializeResponse>.Fail(
                "PLATFORM_NOT_READY", "Platform setup must be completed first.", HttpContext.TraceIdentifier));
        }

        if (_appSetupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<AppSetupInitializeResponse>.Fail(
                "ALREADY_CONFIGURED", "Application setup has already been completed.", HttpContext.TraceIdentifier));
        }

        var requestedConnectionString = ResolveConnectionString(request.Database);
        var requestedDbType = DataSourceDriverRegistry.NormalizeDriverCode(request.Database.DriverCode);
        var runtimeConnectionString = ResolveRuntimeConnectionString(requestedConnectionString, requestedDbType);

        try
        {
            await _appSetupStateProvider.TransitionAsync(AppSetupState.Initializing, cancellationToken: cancellationToken);

            var dbConnected = false;
            var coreTablesVerified = false;
            var verificationErrors = new List<string>();

            try
            {
                using var db = _setupDbClientFactory.Create(runtimeConnectionString, requestedDbType);
                await db.Ado.GetScalarAsync("SELECT 1");
                dbConnected = true;
                _logger.LogInformation("[AppSetup] 数据库连接验证成功");
            }
            catch (Exception dbEx)
            {
                verificationErrors.Add($"Database connection failed: {dbEx.Message}");
                _logger.LogError(dbEx, "[AppSetup] 数据库连接验证失败");
            }

            if (dbConnected)
            {
                try
                {
                    using var db = _setupDbClientFactory.Create(runtimeConnectionString, requestedDbType);
                    var tables = db.DbMaintenance.GetTableInfoList();
                    var tableNames = tables
                        .Select(t => t.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var userTableName = db.EntityMaintenance.GetTableName<UserAccount>();
                    var roleTableName = db.EntityMaintenance.GetTableName<Role>();
                    coreTablesVerified =
                        tableNames.Contains(userTableName) &&
                        tableNames.Contains(roleTableName);

                    if (!coreTablesVerified)
                    {
                        verificationErrors.Add(
                            $"Core tables ({userTableName}, {roleTableName}) not found. Platform initialization may be incomplete.");
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

            await PersistRuntimeConfigAsync(runtimeConnectionString, requestedDbType, cancellationToken);
            await _appSetupStateProvider.CompleteSetupAsync(
                request.AppName,
                request.AdminUsername,
                cancellationToken);

            _logger.LogInformation("[AppSetup] 应用安装完成，AppName={AppName}", request.AppName);

            platformState = ResolvePlatformState();
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

    private async Task PersistRuntimeConfigAsync(string connectionString, string dbType, CancellationToken cancellationToken)
    {
        var runtimeConfigPath = Path.Combine(_environment.ContentRootPath, "appsettings.runtime.json");
        var json = JsonSerializer.Serialize(new
        {
            Database = new { ConnectionString = connectionString, DbType = dbType }
        }, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(runtimeConfigPath, json, cancellationToken);
        _logger.LogInformation("[AppSetup] 数据库配置已持久化到 {Path}", runtimeConfigPath);
    }

    private string ResolveRuntimeConnectionString(string connectionString, string driverCode)
    {
        if (!string.Equals(DataSourceDriverRegistry.NormalizeDriverCode(driverCode), "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var source = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (!Path.IsPathRooted(source))
        {
            source = Path.Combine(_environment.ContentRootPath, source);
        }

        return $"Data Source={source}";
    }

    private static string ResolveConnectionString(AppSetupDatabaseConfigRequest request)
    {
        return DataSourceDriverRegistry.ResolveConnectionString(
            request.DriverCode,
            request.Mode ?? "raw",
            request.ConnectionString,
            request.VisualConfig);
    }

    private static async Task TestSqliteConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task TestGenericConnectionAsync(string connectionString, DbType dbType, CancellationToken cancellationToken)
    {
        var config = new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = dbType,
            IsAutoCloseConnection = true
        };

        using var db = new SqlSugarScope(config);
        await db.Ado.GetScalarAsync("SELECT 1");
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
    AppSetupDatabaseConfigRequest Database,
    string AppName,
    string AdminUsername);

public sealed record AppSetupDatabaseConfigRequest(
    string DriverCode,
    string? Mode,
    string? ConnectionString,
    Dictionary<string, string>? VisualConfig);

public sealed record AppSetupTestConnectionRequest(
    AppSetupDatabaseConfigRequest Database);

public sealed record SetupTestConnectionResponse(
    bool Connected,
    string Message);
