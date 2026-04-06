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
    public async Task<ActionResult<ApiResponse<SetupInitializeResponse>>> Initialize(
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

            // 构建显式参数，不再依赖 AddInMemoryCollection + 冻结的 IOptions
            var bootstrapParams = new SetupBootstrapParams
            {
                ConnectionString = dbPath,
                DbType = driverCode,
                TenantId = request.Admin.TenantId ?? "00000000-0000-0000-0000-000000000001",
                AdminUsername = request.Admin.Username,
                AdminPassword = request.Admin.Password,
                AdminRoles = string.Join(',', BuildAdminRoleCodes(request.Roles?.SelectedRoleCodes)),
                IsPlatformAdmin = true,
                InitialDepartments = SanitizeDepartments(request.Organization?.Departments),
                InitialPositions = SanitizePositions(request.Organization?.Positions),
                SkipSchemaInit = false,
                SkipSeedData = false,
                SkipSchemaMigrations = false
            };

            // 同时更新内存配置，使后续服务（如 DatabaseOptions）也能读到正确的连接串
            var configData = new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = dbPath,
                ["Database:DbType"] = driverCode
            };
            ((IConfigurationBuilder)_configuration).AddInMemoryCollection(configData);

            await _setupStateProvider.TransitionAsync(SetupState.Migrating, cancellationToken: cancellationToken);

            var report = await _databaseInitializer.RunInitializationAsync(bootstrapParams, cancellationToken);

            await _setupStateProvider.TransitionAsync(SetupState.Seeding, cancellationToken: cancellationToken);

            // 持久化数据库配置到 appsettings.runtime.json（重启后回灌）
            await PersistRuntimeConfigAsync(dbPath, driverCode, cancellationToken);

            // 标记安装完成（数据库配置已持久化到 appsettings.runtime.json）
            await _setupStateProvider.CompleteSetupAsync(cancellationToken);

            _logger.LogInformation("[Setup] 平台安装完成，数据库类型: {DbType}", driverCode);

            return Ok(ApiResponse<SetupInitializeResponse>.Ok(
                new SetupInitializeResponse(
                    "Ready",
                    true,
                    report.SchemaInitialized,
                    report.TablesCreated,
                    report.MigrationsApplied,
                    report.MigrationCount,
                    report.SeedCompleted,
                    report.SeedSummary,
                    report.RolesCreated,
                    report.DepartmentsCreated,
                    report.PositionsCreated,
                    report.AdminCreated,
                    report.AdminUsername,
                    report.Errors),
                HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Setup] 平台初始化失败");
            await _setupStateProvider.TransitionAsync(SetupState.Failed, ex.Message, cancellationToken);

            return StatusCode(500, ApiResponse<SetupInitializeResponse>.Fail(
                "SETUP_FAILED", ex.Message, HttpContext.TraceIdentifier));
        }
    }

    private async Task PersistRuntimeConfigAsync(string connectionString, string dbType, CancellationToken cancellationToken)
    {
        var runtimeConfigPath = Path.Combine(_environment.ContentRootPath, "appsettings.runtime.json");
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            Database = new { ConnectionString = connectionString, DbType = dbType }
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(runtimeConfigPath, json, cancellationToken);
        _logger.LogInformation("[Setup] 数据库配置已持久化到 {Path}", runtimeConfigPath);
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
        await db.Ado.GetScalarAsync("SELECT 1");
    }

    private static IReadOnlyList<string> BuildAdminRoleCodes(IReadOnlyList<string>? selectedRoleCodes)
    {
        var roleCodes = new List<string> { "Admin" };
        if (selectedRoleCodes is null)
        {
            return roleCodes;
        }

        foreach (var roleCode in selectedRoleCodes)
        {
            if (string.IsNullOrWhiteSpace(roleCode))
            {
                continue;
            }

            var normalized = roleCode.Trim();
            if (!AllowedSetupRoleCodes.Contains(normalized))
            {
                continue;
            }

            if (!roleCodes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                roleCodes.Add(normalized);
            }
        }

        return roleCodes;
    }

    private static IReadOnlyList<SetupDepartmentSeed> SanitizeDepartments(IReadOnlyList<SetupDepartmentConfigRequest>? departments)
    {
        if (departments is null || departments.Count == 0)
        {
            return [];
        }

        var sanitized = new List<SetupDepartmentSeed>(departments.Count);
        foreach (var department in departments)
        {
            var name = department.Name.Trim();
            var code = string.IsNullOrWhiteSpace(department.Code) ? name : department.Code.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            sanitized.Add(new SetupDepartmentSeed(
                name,
                code,
                string.IsNullOrWhiteSpace(department.ParentCode) ? null : department.ParentCode.Trim(),
                department.SortOrder));
        }

        return sanitized
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<SetupPositionSeed> SanitizePositions(IReadOnlyList<SetupPositionConfigRequest>? positions)
    {
        if (positions is null || positions.Count == 0)
        {
            return [];
        }

        var sanitized = new List<SetupPositionSeed>(positions.Count);
        foreach (var position in positions)
        {
            var name = position.Name.Trim();
            var code = position.Code.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            sanitized.Add(new SetupPositionSeed(
                name,
                code,
                string.IsNullOrWhiteSpace(position.Description) ? null : position.Description.Trim(),
                position.SortOrder));
        }

        return sanitized
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static readonly HashSet<string> AllowedSetupRoleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SecurityAdmin",
        "AuditAdmin",
        "AssetAdmin",
        "ApprovalAdmin"
    };
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
    SetupAdminConfigRequest Admin,
    SetupRoleConfigRequest? Roles,
    SetupOrganizationConfigRequest? Organization);

public sealed record SetupDatabaseConfigRequest(
    string DriverCode,
    string? Mode,
    string? ConnectionString,
    Dictionary<string, string>? VisualConfig);

public sealed record SetupAdminConfigRequest(
    string? TenantId,
    string Username,
    string Password);

public sealed record SetupRoleConfigRequest(
    IReadOnlyList<string>? SelectedRoleCodes);

public sealed record SetupOrganizationConfigRequest(
    IReadOnlyList<SetupDepartmentConfigRequest>? Departments,
    IReadOnlyList<SetupPositionConfigRequest>? Positions);

public sealed record SetupDepartmentConfigRequest(
    string Name,
    string? Code,
    string? ParentCode,
    int SortOrder);

public sealed record SetupPositionConfigRequest(
    string Name,
    string Code,
    string? Description,
    int SortOrder);

public sealed record SetupInitializeResponse(
    string Status,
    bool PlatformSetupCompleted,
    bool SchemaInitialized,
    int TablesCreated,
    bool MigrationsApplied,
    int MigrationCount,
    bool SeedCompleted,
    string SeedSummary,
    int RolesCreated,
    int DepartmentsCreated,
    int PositionsCreated,
    bool AdminCreated,
    string? AdminUsername,
    List<string> Errors);
