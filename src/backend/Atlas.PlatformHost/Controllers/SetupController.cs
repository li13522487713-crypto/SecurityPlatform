using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Setup;
using Atlas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using SqlSugar;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 平台安装向导控制器。
/// setup 未完成时，此控制器是唯一可访问的业务端点（由 SetupModeMiddleware 保证）。
/// </summary>
[ApiController]
[Route("api/v1/setup")]
[AllowAnonymous]
public sealed class SetupController : ControllerBase
{
    private readonly ISetupStateProvider _setupStateProvider;
    private readonly DatabaseInitializerHostedService _databaseInitializer;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SetupController> _logger;

    public SetupController(
        ISetupStateProvider setupStateProvider,
        DatabaseInitializerHostedService databaseInitializer,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<SetupController> logger)
    {
        _setupStateProvider = setupStateProvider;
        _databaseInitializer = databaseInitializer;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>获取当前安装状态</summary>
    [HttpGet("state")]
    public ActionResult<ApiResponse<SetupStateResponse>> GetState()
    {
        var state = _setupStateProvider.GetState();
        var response = new SetupStateResponse(
            state.Status.ToString(),
            state.PlatformSetupCompleted,
            state.CompletedAt,
            state.FailureMessage);

        return Ok(ApiResponse<SetupStateResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>获取可用的数据库驱动列表</summary>
    [HttpGet("drivers")]
    public ActionResult<ApiResponse<IReadOnlyList<DataSourceDriverDefinition>>> GetDrivers()
    {
        var drivers = DataSourceDriverRegistry.GetDefinitions();
        return Ok(ApiResponse<IReadOnlyList<DataSourceDriverDefinition>>.Ok(drivers, HttpContext.TraceIdentifier));
    }

    /// <summary>测试数据库连接</summary>
    [HttpPost("test-connection")]
    public async Task<ActionResult<ApiResponse<SetupTestConnectionResponse>>> TestConnection(
        [FromBody] SetupTestConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (_setupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<SetupTestConnectionResponse>.Fail(
                "ALREADY_CONFIGURED", "Platform setup has already been completed.", HttpContext.TraceIdentifier));
        }

        try
        {
            var connectionString = ResolveConnectionString(request);
            var dbType = DataSourceDriverRegistry.ResolveDbType(request.DriverCode);

            if (dbType == DbType.Sqlite)
            {
                await TestSqliteConnectionAsync(connectionString, cancellationToken);
            }
            else
            {
                await TestGenericConnectionAsync(connectionString, dbType, cancellationToken);
            }

            return Ok(ApiResponse<SetupTestConnectionResponse>.Ok(
                new SetupTestConnectionResponse(true, "Connection successful."), HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Setup] 数据库连接测试失败");
            return Ok(ApiResponse<SetupTestConnectionResponse>.Ok(
                new SetupTestConnectionResponse(false, ex.Message), HttpContext.TraceIdentifier));
        }
    }

    /// <summary>执行完整初始化（建表/迁移/种子/管理员）并标记安装完成</summary>
    [HttpPost("initialize")]
    public async Task<ActionResult<ApiResponse<SetupStateResponse>>> Initialize(
        [FromBody] SetupInitializeRequest request,
        CancellationToken cancellationToken)
    {
        if (_setupStateProvider.IsReady)
        {
            return BadRequest(ApiResponse<SetupStateResponse>.Fail(
                "ALREADY_CONFIGURED", "Platform setup has already been completed.", HttpContext.TraceIdentifier));
        }

        try
        {
            var connectionString = ResolveConnectionString(request.Database);
            var driverCode = DataSourceDriverRegistry.NormalizeDriverCode(request.Database.DriverCode);

            await _setupStateProvider.TransitionAsync(SetupState.Configuring, cancellationToken: cancellationToken);

            // 写入数据库配置到 appsettings 覆盖（通过内存配置 + setup-state.json 持久化）
            var dbPath = connectionString;
            if (string.Equals(driverCode, "SQLite", StringComparison.OrdinalIgnoreCase))
            {
                var source = new SqliteConnectionStringBuilder(connectionString).DataSource;
                if (!Path.IsPathRooted(source))
                {
                    source = Path.Combine(_environment.ContentRootPath, source);
                }
                dbPath = $"Data Source={source}";
            }

            // 将管理员配置写入内存配置供 DatabaseInitializer 使用
            var configData = new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = dbPath,
                ["Database:DbType"] = driverCode,
                ["Security:BootstrapAdmin:Enabled"] = "true",
                ["Security:BootstrapAdmin:TenantId"] = request.Admin.TenantId ?? "00000000-0000-0000-0000-000000000001",
                ["Security:BootstrapAdmin:Username"] = request.Admin.Username,
                ["Security:BootstrapAdmin:Password"] = request.Admin.Password,
                ["Security:BootstrapAdmin:Roles"] = "Admin",
                ["Security:BootstrapAdmin:IsPlatformAdmin"] = "true",
                ["DatabaseInitializer:SkipSchemaInit"] = "false",
                ["DatabaseInitializer:SkipSeedData"] = "false",
                ["DatabaseInitializer:SkipSchemaMigrations"] = "false"
            };

            ((IConfigurationBuilder)_configuration).AddInMemoryCollection(configData);

            await _setupStateProvider.TransitionAsync(SetupState.Migrating, cancellationToken: cancellationToken);

            // 执行数据库初始化（建表/迁移/种子/管理员）
            await _databaseInitializer.RunInitializationAsync(cancellationToken);

            await _setupStateProvider.TransitionAsync(SetupState.Seeding, cancellationToken: cancellationToken);

            // 标记安装完成
            var dbInfo = new SetupDatabaseInfo
            {
                DbType = driverCode,
                ConnectionString = dbPath
            };
            await _setupStateProvider.CompleteSetupAsync(dbInfo, cancellationToken);

            var state = _setupStateProvider.GetState();
            var response = new SetupStateResponse(
                state.Status.ToString(),
                state.PlatformSetupCompleted,
                state.CompletedAt,
                state.FailureMessage);

            _logger.LogInformation("[Setup] 平台安装完成，数据库类型: {DbType}", driverCode);

            return Ok(ApiResponse<SetupStateResponse>.Ok(response, HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Setup] 平台初始化失败");
            await _setupStateProvider.TransitionAsync(SetupState.Failed, ex.Message, cancellationToken);

            return StatusCode(500, ApiResponse<SetupStateResponse>.Fail(
                "SETUP_FAILED", ex.Message, HttpContext.TraceIdentifier));
        }
    }

    private static string ResolveConnectionString(SetupTestConnectionRequest request)
    {
        return DataSourceDriverRegistry.ResolveConnectionString(
            request.DriverCode,
            request.Mode ?? "raw",
            request.ConnectionString,
            request.VisualConfig);
    }

    private static string ResolveConnectionString(SetupDatabaseConfigRequest request)
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
        await db.Ado.GetScalarAsync("SELECT 1", cancellationToken);
    }
}

// --- Request/Response DTOs ---

public sealed record SetupStateResponse(
    string Status,
    bool PlatformSetupCompleted,
    DateTimeOffset? CompletedAt,
    string? FailureMessage);

public sealed record SetupTestConnectionRequest(
    string DriverCode,
    string? Mode,
    string? ConnectionString,
    Dictionary<string, string>? VisualConfig);

public sealed record SetupTestConnectionResponse(bool Connected, string Message);

public sealed record SetupInitializeRequest(
    SetupDatabaseConfigRequest Database,
    SetupAdminConfigRequest Admin);

public sealed record SetupDatabaseConfigRequest(
    string DriverCode,
    string? Mode,
    string? ConnectionString,
    Dictionary<string, string>? VisualConfig);

public sealed record SetupAdminConfigRequest(
    string? TenantId,
    string Username,
    string Password);
