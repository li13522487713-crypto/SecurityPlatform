using Atlas.Application.System.Models;
using Atlas.AppHost.Sdk.Hosting;
using Atlas.Core.Abstractions;
using Atlas.Core.Enums;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Setup;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Setup;
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
    private static readonly Guid DefaultTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ISetupStateProvider _platformSetupStateProvider;
    private readonly IAppSetupStateProvider _appSetupStateProvider;
    private readonly ISetupDbClientFactory _setupDbClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ILogger<SetupStateController> _logger;

    public SetupStateController(
        ISetupStateProvider platformSetupStateProvider,
        IAppSetupStateProvider appSetupStateProvider,
        ISetupDbClientFactory setupDbClientFactory,
        IConfiguration configuration,
        IHostEnvironment environment,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAppContextAccessor appContextAccessor,
        ILogger<SetupStateController> logger)
    {
        _platformSetupStateProvider = platformSetupStateProvider;
        _appSetupStateProvider = appSetupStateProvider;
        _setupDbClientFactory = setupDbClientFactory;
        _configuration = configuration;
        _environment = environment;
        _idGeneratorAccessor = idGeneratorAccessor;
        _appContextAccessor = appContextAccessor;
        _logger = logger;
    }

    /// <summary>获取平台和应用两级 setup 状态</summary>
    [HttpGet("state")]
    public async Task<ActionResult<ApiResponse<AppHostSetupStateResponse>>> GetState(CancellationToken cancellationToken)
    {
        var platformState = ResolvePlatformState();
        if (!IsPlatformReady(platformState))
        {
            var configuredDatabase = TryResolveConfiguredDatabase();
            if (configuredDatabase is not null)
            {
                platformState = await EnsurePlatformStateReadyAsync(
                    configuredDatabase.Value.ConnectionString,
                    configuredDatabase.Value.DbType,
                    preferredAdminUsername: null,
                    cancellationToken);
            }
        }

        var platformReady = IsPlatformReady(platformState);
        var appState = _appSetupStateProvider.GetState();
        return Ok(ApiResponse<AppHostSetupStateResponse>.Ok(
            new AppHostSetupStateResponse(
                platformReady ? SetupState.Ready.ToString() : platformState.Status.ToString(),
                platformReady,
                appState.Status.ToString(),
                _appSetupStateProvider.IsReady,
                appState.AppKey),
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

    private static bool IsPlatformReady(SetupStateInfo state)
    {
        return state.Status == SetupState.Ready || state.PlatformSetupCompleted;
    }

    private (string ConnectionString, string DbType)? TryResolveConfiguredDatabase()
    {
        var connectionString = _configuration["Database:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var dbType = string.IsNullOrWhiteSpace(_configuration["Database:DbType"])
            ? "SQLite"
            : _configuration["Database:DbType"]!;
        var runtimeConnectionString = ResolveRuntimeConnectionString(connectionString, dbType);
        return (runtimeConnectionString, DataSourceDriverRegistry.NormalizeDriverCode(dbType));
    }

    private async Task<SetupStateInfo> EnsurePlatformStateReadyAsync(
        string connectionString,
        string dbType,
        string? preferredAdminUsername,
        CancellationToken cancellationToken)
    {
        var platformState = ResolvePlatformState();
        if (IsPlatformReady(platformState))
        {
            return platformState;
        }

        var inferencePassed = await CanInferPlatformReadyAsync(connectionString, dbType, preferredAdminUsername, cancellationToken);
        if (!inferencePassed)
        {
            return platformState;
        }

        _logger.LogWarning(
            "[AppSetup] 检测到平台 setup-state 未就绪，但数据库已具备平台初始化条件，尝试自动修复为 Ready。");

        try
        {
            await _platformSetupStateProvider.CompleteSetupAsync(cancellationToken);
            var repairedState = ResolvePlatformState();
            if (IsPlatformReady(repairedState))
            {
                _logger.LogInformation("[AppSetup] 平台 setup-state 自动修复成功。");
                return repairedState;
            }

            _logger.LogWarning("[AppSetup] 平台 setup-state 自动修复后仍非 Ready，将按未就绪处理。");
            return repairedState;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AppSetup] 平台 setup-state 自动修复失败");
            return platformState;
        }
    }

    private async Task<bool> CanInferPlatformReadyAsync(
        string connectionString,
        string dbType,
        string? preferredAdminUsername,
        CancellationToken cancellationToken)
    {
        try
        {
            using var db = _setupDbClientFactory.Create(connectionString, dbType);
            await db.Ado.GetScalarAsync("SELECT 1");

            var tables = db.DbMaintenance.GetTableInfoList();
            var tableNames = tables
                .Select(t => t.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var userTableName = db.EntityMaintenance.GetTableName<UserAccount>();
            var roleTableName = db.EntityMaintenance.GetTableName<Role>();
            if (!tableNames.Contains(userTableName) || !tableNames.Contains(roleTableName))
            {
                return false;
            }

            var tenantId = ResolveTenantId();
            var userQuery = db.Queryable<UserAccount>()
                .Where(user => user.TenantIdValue == tenantId.Value);
            if (!string.IsNullOrWhiteSpace(preferredAdminUsername))
            {
                var normalizedUsername = preferredAdminUsername.Trim();
                userQuery = userQuery.Where(user => user.Username == normalizedUsername);
            }

            var hasAdminUser = await userQuery.AnyAsync();
            if (!hasAdminUser)
            {
                return false;
            }

            var hasRoles = await db.Queryable<Role>()
                .AnyAsync(role => role.TenantIdValue == tenantId.Value, cancellationToken);
            return hasRoles;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[AppSetup] 平台状态推断失败");
            return false;
        }
    }

    /// <summary>
    /// 执行应用级初始化：验证数据库连接、确认平台已初始化、创建/绑定应用实例并播种应用组织数据。
    /// </summary>
    [HttpPost("initialize")]
    public async Task<ActionResult<ApiResponse<AppSetupInitializeResponse>>> Initialize(
        [FromBody] AppSetupInitializeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Admin.AppName) || string.IsNullOrWhiteSpace(request.Admin.AdminUsername))
        {
            return BadRequest(ApiResponse<AppSetupInitializeResponse>.Fail(
                "INVALID_REQUEST",
                "App name and admin username are required.",
                HttpContext.TraceIdentifier));
        }

        var requestedConnectionString = ResolveConnectionString(request.Database);
        var requestedDbType = DataSourceDriverRegistry.NormalizeDriverCode(request.Database.DriverCode);
        var runtimeConnectionString = ResolveRuntimeConnectionString(requestedConnectionString, requestedDbType);
        var configuredPlatformDatabase = TryResolveConfiguredDatabase();
        var platformConnectionString = configuredPlatformDatabase?.ConnectionString ?? runtimeConnectionString;
        var platformDbType = configuredPlatformDatabase?.DbType ?? requestedDbType;

        var platformState = await EnsurePlatformStateReadyAsync(
            platformConnectionString,
            platformDbType,
            request.Admin.AdminUsername,
            cancellationToken);
        if (!IsPlatformReady(platformState))
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
            var rolesCreated = 0;
            var departmentsCreated = 0;
            var positionsCreated = 0;
            var adminBound = false;

            using var db = _setupDbClientFactory.Create(runtimeConnectionString, requestedDbType);
            using var platformDb = _setupDbClientFactory.Create(platformConnectionString, platformDbType);

            try
            {
                await db.Ado.GetScalarAsync("SELECT 1");
                dbConnected = true;
                _logger.LogInformation("[AppSetup] 应用数据库连接验证成功");
            }
            catch (Exception dbEx)
            {
                verificationErrors.Add($"Database connection failed: {dbEx.Message}");
                _logger.LogError(dbEx, "[AppSetup] 应用数据库连接验证失败");
            }

            if (dbConnected)
            {
                try
                {
                    db.CodeFirst.InitTables(
                        typeof(LowCodeApp),
                        typeof(AppDataRoutePolicy),
                        typeof(AppRole),
                        typeof(AppDepartment),
                        typeof(AppPosition),
                        typeof(AppMember),
                        typeof(AppUserRole),
                        typeof(AppMemberDepartment),
                        typeof(AppMemberPosition));
                    _logger.LogInformation("[AppSetup] 应用核心表初始化完成");
                }
                catch (Exception appSchemaEx)
                {
                    verificationErrors.Add($"App schema initialization failed: {appSchemaEx.Message}");
                    _logger.LogError(appSchemaEx, "[AppSetup] 应用核心表初始化失败");
                }
            }

            try
            {
                await platformDb.Ado.GetScalarAsync("SELECT 1");
            }
            catch (Exception platformDbEx)
            {
                verificationErrors.Add($"Platform database connection failed: {platformDbEx.Message}");
                _logger.LogError(platformDbEx, "[AppSetup] 平台数据库连接验证失败");
            }

            if (verificationErrors.Count == 0)
            {
                try
                {
                    var tables = platformDb.DbMaintenance.GetTableInfoList();
                    var tableNames = tables
                        .Select(t => t.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var userTableName = platformDb.EntityMaintenance.GetTableName<UserAccount>();
                    var roleTableName = platformDb.EntityMaintenance.GetTableName<Role>();
                    coreTablesVerified =
                        tableNames.Contains(userTableName) &&
                        tableNames.Contains(roleTableName);

                    if (!coreTablesVerified)
                    {
                        verificationErrors.Add(
                            $"Core tables ({userTableName}, {roleTableName}) not found in platform database. Platform initialization may be incomplete.");
                    }
                    else
                    {
                        _logger.LogInformation("[AppSetup] 平台核心表验证通过，共 {Count} 张表", tables.Count);
                    }
                }
                catch (Exception tableEx)
                {
                    verificationErrors.Add($"Platform table verification failed: {tableEx.Message}");
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

            var tenantId = ResolveTenantId();
            var adminUsername = request.Admin.AdminUsername.Trim();
            var appName = request.Admin.AppName.Trim();
            var appKey = !string.IsNullOrWhiteSpace(request.Admin.AppKey)
                ? request.Admin.AppKey.Trim()
                : ResolveSetupAppKey();

            var adminUser = await platformDb.Queryable<UserAccount>()
                .FirstAsync(
                    user => user.TenantIdValue == tenantId.Value && user.Username == adminUsername,
                    cancellationToken);
            if (adminUser is null)
            {
                var missingAdminMessage = $"App admin user '{adminUsername}' was not found in platform users.";
                await _appSetupStateProvider.TransitionAsync(AppSetupState.Failed, missingAdminMessage, cancellationToken);
                return BadRequest(ApiResponse<AppSetupInitializeResponse>.Fail(
                    "APP_ADMIN_NOT_FOUND",
                    missingAdminMessage,
                    HttpContext.TraceIdentifier));
            }

            var selectedRoleCodes = BuildAppRoleCodes(request.Roles?.SelectedRoleCodes);
            var departments = SanitizeDepartments(request.Organization?.Departments);
            var positions = SanitizePositions(request.Organization?.Positions);

            AppOrganizationSeedResult seedResult;
            LowCodeApp app;
            using (BeginSetupScope(tenantId, appKey))
            {
                app = await EnsureAppInstanceAsync(db, tenantId, appKey, appName, adminUser.Id, cancellationToken);
                await EnsureMainOnlyRoutePolicyAsync(db, tenantId, app.Id, adminUser.Id, cancellationToken);

                // 当应用库与平台主库不同时，AppDbScopeFactory 从主库查询 AppDataRoutePolicy 来判断路由模式。
                // 必须将 LowCodeApp 与 AppDataRoutePolicy 同步写入平台主库，否则动态表等依赖 GetAppClientAsync 的功能
                // 会因找不到 MainOnly 策略而尝试查找 TenantAppDataSourceBinding，导致 "未绑定可用数据源" 异常。
                if (!AreSameDatabase(runtimeConnectionString, platformConnectionString))
                {
                    await SyncAppInstanceToPlatformDbAsync(platformDb, tenantId, app, cancellationToken);
                    await EnsureMainOnlyRoutePolicyAsync(platformDb, tenantId, app.Id, adminUser.Id, cancellationToken);
                    _logger.LogInformation(
                        "[AppSetup] 应用实例与路由策略已同步到平台主库 (AppId={AppId})", app.Id);
                }

                seedResult = await SeedApplicationOrganizationAsync(
                    db,
                    tenantId,
                    app.Id,
                    adminUser.Id,
                    selectedRoleCodes,
                    departments,
                    positions,
                    cancellationToken);
            }

            rolesCreated = seedResult.RolesCreated;
            departmentsCreated = seedResult.DepartmentsCreated;
            positionsCreated = seedResult.PositionsCreated;
            adminBound = seedResult.AdminBound;

            await PersistRuntimeConfigAsync(runtimeConnectionString, requestedDbType, cancellationToken);
            await _appSetupStateProvider.CompleteSetupAsync(appName, adminUsername, appKey, cancellationToken);

            _logger.LogInformation(
                "[AppSetup] 应用安装完成，AppName={AppName}, AppId={AppId}, RolesCreated={RolesCreated}, DepartmentsCreated={DepartmentsCreated}, PositionsCreated={PositionsCreated}, AdminBound={AdminBound}",
                appName,
                app.Id,
                rolesCreated,
                departmentsCreated,
                positionsCreated,
                adminBound);

            platformState = ResolvePlatformState();
            var platformReady = IsPlatformReady(platformState);
            var appState = _appSetupStateProvider.GetState();
            return Ok(ApiResponse<AppSetupInitializeResponse>.Ok(
                new AppSetupInitializeResponse(
                    platformReady ? SetupState.Ready.ToString() : platformState.Status.ToString(),
                    platformReady,
                    appState.Status.ToString(),
                    true,
                    dbConnected,
                    coreTablesVerified,
                    rolesCreated,
                    departmentsCreated,
                    positionsCreated,
                    adminBound,
                    verificationErrors,
                    appKey),
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

    private async Task<LowCodeApp> EnsureAppInstanceAsync(
        ISqlSugarClient db,
        TenantId tenantId,
        string appKey,
        string appName,
        long adminUserId,
        CancellationToken cancellationToken)
    {
        var existing = await db.Queryable<LowCodeApp>()
            .FirstAsync(
                app => app.TenantIdValue == tenantId.Value && app.AppKey == appKey,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var app = new LowCodeApp(
            tenantId,
            appKey,
            appName,
            description: null,
            category: null,
            icon: null,
            createdBy: adminUserId,
            id: _idGeneratorAccessor.NextId(),
            now: now);

        await db.Insertable(app).ExecuteCommandAsync(cancellationToken);
        _logger.LogInformation("[AppSetup] AppKey={AppKey} 未命中实例，已自动创建 AppId={AppId}", appKey, app.Id);
        return app;
    }

    private async Task EnsureMainOnlyRoutePolicyAsync(
        ISqlSugarClient db,
        TenantId tenantId,
        long appId,
        long updatedBy,
        CancellationToken cancellationToken)
    {
        var policy = await db.Queryable<AppDataRoutePolicy>()
            .FirstAsync(
                item => item.TenantIdValue == tenantId.Value && item.AppInstanceId == appId,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (policy is null)
        {
            policy = new AppDataRoutePolicy(
                tenantId,
                appId,
                mode: "MainOnly",
                readOnlyWindow: false,
                dualWriteEnabled: false,
                updatedBy,
                _idGeneratorAccessor.NextId(),
                now);
            await db.Insertable(policy).ExecuteCommandAsync(cancellationToken);
            return;
        }

        if (string.Equals(policy.Mode, "MainOnly", StringComparison.OrdinalIgnoreCase)
            && !policy.ReadOnlyWindow
            && !policy.DualWriteEnabled)
        {
            return;
        }

        policy.SetMode("MainOnly", readOnlyWindow: false, dualWriteEnabled: false, updatedBy, now);
        await db.Updateable(policy).ExecuteCommandAsync(cancellationToken);
    }

    private async Task SyncAppInstanceToPlatformDbAsync(
        ISqlSugarClient platformDb,
        TenantId tenantId,
        LowCodeApp app,
        CancellationToken cancellationToken)
    {
        var existing = await platformDb.Queryable<LowCodeApp>()
            .FirstAsync(
                item => item.TenantIdValue == tenantId.Value && item.Id == app.Id,
                cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var existByAppKey = await platformDb.Queryable<LowCodeApp>()
            .FirstAsync(
                item => item.TenantIdValue == tenantId.Value && item.AppKey == app.AppKey,
                cancellationToken);
        if (existByAppKey is not null)
        {
            return;
        }

        await platformDb.Insertable(app).ExecuteCommandAsync(cancellationToken);
    }

    private static bool AreSameDatabase(string connectionStringA, string connectionStringB)
    {
        return string.Equals(
            connectionStringA?.Trim(),
            connectionStringB?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private IDisposable BeginSetupScope(TenantId tenantId, string appId)
    {
        var currentContext = _appContextAccessor.GetCurrent();
        var scopeContext = new AppContextSnapshot(
            tenantId,
            appId,
            currentContext.CurrentUser,
            currentContext.ClientContext,
            HttpContext.TraceIdentifier);
        return _appContextAccessor.BeginScope(scopeContext);
    }

    private string ResolveSetupAppKey()
    {
        var loader = new AppInstanceConfigurationLoader(_configuration);
        var appKey = loader.Load().AppKey?.Trim();
        if (!string.IsNullOrWhiteSpace(appKey))
        {
            return appKey;
        }

        var configuredAppKey = _configuration["Atlas:AppHost:AppKey"];
        return string.IsNullOrWhiteSpace(configuredAppKey) ? "app-default" : configuredAppKey.Trim();
    }

    private async Task<AppOrganizationSeedResult> SeedApplicationOrganizationAsync(
        ISqlSugarClient db,
        TenantId tenantId,
        long appId,
        long adminUserId,
        IReadOnlyList<string> selectedRoleCodes,
        IReadOnlyList<AppSetupDepartmentSeed> requestedDepartments,
        IReadOnlyList<AppSetupPositionSeed> requestedPositions,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rolesCreated = 0;
        var departmentsCreated = 0;
        var positionsCreated = 0;

        var roleCodes = selectedRoleCodes.Count > 0
            ? selectedRoleCodes
            : ["AppAdmin", "AppMember"];
        var roleCodeArray = roleCodes.ToArray();

        var existingRoles = await db.Queryable<AppRole>()
            .Where(role =>
                role.TenantIdValue == tenantId.Value
                && role.AppId == appId
                && SqlFunc.ContainsArray(roleCodeArray, role.Code))
            .ToListAsync(cancellationToken);

        var roleByCode = existingRoles.ToDictionary(role => role.Code, StringComparer.OrdinalIgnoreCase);
        var newRoles = new List<AppRole>();
        foreach (var roleCode in roleCodes)
        {
            if (roleByCode.ContainsKey(roleCode))
            {
                continue;
            }

            var role = BuildAppRoleEntity(tenantId, appId, roleCode, adminUserId, now, _idGeneratorAccessor.NextId());
            newRoles.Add(role);
            roleByCode[role.Code] = role;
        }

        if (newRoles.Count > 0)
        {
            await db.Insertable(newRoles).ExecuteCommandAsync(cancellationToken);
            rolesCreated = newRoles.Count;
        }

        var departmentSeeds = requestedDepartments.Count > 0
            ? requestedDepartments
            : [new AppSetupDepartmentSeed("默认部门", "ROOT", null, 0)];
        var departmentCodeArray = departmentSeeds
            .Select(seed => seed.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingDepartments = departmentCodeArray.Length == 0
            ? []
            : await db.Queryable<AppDepartment>()
                .Where(department =>
                    department.TenantIdValue == tenantId.Value
                    && department.AppId == appId
                    && SqlFunc.ContainsArray(departmentCodeArray, department.Code))
                .ToListAsync(cancellationToken);

        var departmentByCode = existingDepartments.ToDictionary(department => department.Code, StringComparer.OrdinalIgnoreCase);
        var pendingDepartments = departmentSeeds
            .Where(seed => !departmentByCode.ContainsKey(seed.Code))
            .ToList();
        var newDepartments = new List<AppDepartment>();

        while (pendingDepartments.Count > 0)
        {
            var progressed = false;

            for (var index = pendingDepartments.Count - 1; index >= 0; index--)
            {
                var seed = pendingDepartments[index];
                var departmentId = _idGeneratorAccessor.NextId();
                long? parentId = departmentId;
                if (!string.IsNullOrWhiteSpace(seed.ParentCode))
                {
                    if (!departmentByCode.TryGetValue(seed.ParentCode, out var parentDepartment))
                    {
                        continue;
                    }

                    parentId = parentDepartment.Id;
                }

                var department = new AppDepartment(
                    tenantId,
                    appId,
                    seed.Name,
                    seed.Code,
                    parentId,
                    seed.SortOrder,
                    departmentId);
                newDepartments.Add(department);
                departmentByCode[department.Code] = department;
                pendingDepartments.RemoveAt(index);
                progressed = true;
            }

            if (progressed)
            {
                continue;
            }

            foreach (var unresolved in pendingDepartments)
            {
                var departmentId = _idGeneratorAccessor.NextId();
                var department = new AppDepartment(
                    tenantId,
                    appId,
                    unresolved.Name,
                    unresolved.Code,
                    parentId: departmentId,
                    unresolved.SortOrder,
                    departmentId);
                newDepartments.Add(department);
                departmentByCode[department.Code] = department;
            }

            pendingDepartments.Clear();
        }

        if (newDepartments.Count > 0)
        {
            await db.Insertable(newDepartments).ExecuteCommandAsync(cancellationToken);
            departmentsCreated = newDepartments.Count;
        }

        var positionSeeds = requestedPositions.Count > 0
            ? requestedPositions
            : [new AppSetupPositionSeed("系统管理员", "APP_SYS_ADMIN", "应用初始化默认岗位", 10)];
        var positionCodeArray = positionSeeds
            .Select(seed => seed.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingPositions = positionCodeArray.Length == 0
            ? []
            : await db.Queryable<AppPosition>()
                .Where(position =>
                    position.TenantIdValue == tenantId.Value
                    && position.AppId == appId
                    && SqlFunc.ContainsArray(positionCodeArray, position.Code))
                .ToListAsync(cancellationToken);

        var positionByCode = existingPositions.ToDictionary(position => position.Code, StringComparer.OrdinalIgnoreCase);
        var newPositions = new List<AppPosition>();
        foreach (var seed in positionSeeds)
        {
            if (positionByCode.ContainsKey(seed.Code))
            {
                continue;
            }

            var position = new AppPosition(
                tenantId,
                appId,
                seed.Name,
                seed.Code,
                _idGeneratorAccessor.NextId());
            position.Update(seed.Name, seed.Description, isActive: true, seed.SortOrder);
            newPositions.Add(position);
            positionByCode[position.Code] = position;
        }

        if (newPositions.Count > 0)
        {
            await db.Insertable(newPositions).ExecuteCommandAsync(cancellationToken);
            positionsCreated = newPositions.Count;
        }

        var member = await db.Queryable<AppMember>()
            .FirstAsync(
                item => item.TenantIdValue == tenantId.Value && item.AppId == appId && item.UserId == adminUserId,
                cancellationToken);
        if (member is null)
        {
            member = new AppMember(
                tenantId,
                appId,
                adminUserId,
                adminUserId,
                now,
                _idGeneratorAccessor.NextId());
            await db.Insertable(member).ExecuteCommandAsync(cancellationToken);
        }

        var roleIdsToBind = roleByCode.Values
            .Select(role => role.Id)
            .Distinct()
            .ToArray();

        if (roleIdsToBind.Length > 0)
        {
            var existingUserRoles = await db.Queryable<AppUserRole>()
                .Where(item =>
                    item.TenantIdValue == tenantId.Value
                    && item.AppId == appId
                    && item.UserId == adminUserId
                    && SqlFunc.ContainsArray(roleIdsToBind, item.RoleId))
                .ToListAsync(cancellationToken);
            var existingRoleIds = existingUserRoles
                .Select(item => item.RoleId)
                .ToHashSet();

            var userRolesToInsert = roleIdsToBind
                .Where(roleId => !existingRoleIds.Contains(roleId))
                .Select(roleId => new AppUserRole(tenantId, appId, adminUserId, roleId, _idGeneratorAccessor.NextId()))
                .ToList();
            if (userRolesToInsert.Count > 0)
            {
                await db.Insertable(userRolesToInsert).ExecuteCommandAsync(cancellationToken);
            }
        }

        var primaryDepartment = departmentByCode.Values
            .OrderBy(department => department.SortOrder)
            .ThenBy(department => department.Id)
            .FirstOrDefault();
        if (primaryDepartment is not null)
        {
            var existingMemberDepartment = await db.Queryable<AppMemberDepartment>()
                .FirstAsync(
                    item => item.TenantIdValue == tenantId.Value
                        && item.AppId == appId
                        && item.UserId == adminUserId
                        && item.DepartmentId == primaryDepartment.Id,
                    cancellationToken);
            if (existingMemberDepartment is null)
            {
                var memberDepartment = new AppMemberDepartment(
                    tenantId,
                    appId,
                    adminUserId,
                    primaryDepartment.Id,
                    isPrimary: true,
                    _idGeneratorAccessor.NextId());
                await db.Insertable(memberDepartment).ExecuteCommandAsync(cancellationToken);
            }
        }

        var primaryPosition = positionByCode.Values
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Id)
            .FirstOrDefault();
        if (primaryPosition is not null)
        {
            var existingMemberPosition = await db.Queryable<AppMemberPosition>()
                .FirstAsync(
                    item => item.TenantIdValue == tenantId.Value
                        && item.AppId == appId
                        && item.UserId == adminUserId
                        && item.PositionId == primaryPosition.Id,
                    cancellationToken);
            if (existingMemberPosition is null)
            {
                var memberPosition = new AppMemberPosition(
                    tenantId,
                    appId,
                    adminUserId,
                    primaryPosition.Id,
                    isPrimary: true,
                    _idGeneratorAccessor.NextId());
                await db.Insertable(memberPosition).ExecuteCommandAsync(cancellationToken);
            }
        }

        var hasMember = await db.Queryable<AppMember>()
            .AnyAsync(item => item.TenantIdValue == tenantId.Value && item.AppId == appId && item.UserId == adminUserId);
        var appAdminRoleId = roleByCode.TryGetValue("AppAdmin", out var appAdminRole)
            ? appAdminRole.Id
            : 0;
        var hasAdminRoleBinding = appAdminRoleId > 0 && await db.Queryable<AppUserRole>()
            .AnyAsync(item =>
                item.TenantIdValue == tenantId.Value
                && item.AppId == appId
                && item.UserId == adminUserId
                && item.RoleId == appAdminRoleId);

        return new AppOrganizationSeedResult(
            rolesCreated,
            departmentsCreated,
            positionsCreated,
            hasMember && hasAdminRoleBinding);
    }

    private static AppRole BuildAppRoleEntity(
        TenantId tenantId,
        long appId,
        string roleCode,
        long createdBy,
        DateTimeOffset createdAt,
        long id)
    {
        if (string.Equals(roleCode, "AppAdmin", StringComparison.OrdinalIgnoreCase))
        {
            var role = new AppRole(
                tenantId,
                appId,
                "AppAdmin",
                "应用管理员",
                "应用全局管理员，拥有全部权限",
                isSystem: true,
                createdBy,
                createdAt,
                id);
            role.SetDataScope(DataScopeType.All);
            return role;
        }

        if (string.Equals(roleCode, "AppMember", StringComparison.OrdinalIgnoreCase))
        {
            var role = new AppRole(
                tenantId,
                appId,
                "AppMember",
                "应用成员",
                "应用普通成员，仅可访问本人数据",
                isSystem: true,
                createdBy,
                createdAt,
                id);
            role.SetDataScope(DataScopeType.OnlySelf);
            return role;
        }

        if (OptionalRoleTemplates.TryGetValue(roleCode, out var template))
        {
            var role = new AppRole(
                tenantId,
                appId,
                template.Code,
                template.Name,
                template.Description,
                isSystem: false,
                createdBy,
                createdAt,
                id);
            role.SetDataScope(template.DataScope);
            return role;
        }

        var fallbackRole = new AppRole(
            tenantId,
            appId,
            roleCode,
            roleCode,
            $"{roleCode} role created by setup wizard.",
            isSystem: false,
            createdBy,
            createdAt,
            id);
        fallbackRole.SetDataScope(DataScopeType.CurrentTenant);
        return fallbackRole;
    }

    private static IReadOnlyList<string> BuildAppRoleCodes(IReadOnlyList<string>? selectedRoleCodes)
    {
        var roleCodes = new List<string> { "AppAdmin", "AppMember" };
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

    private static IReadOnlyList<AppSetupDepartmentSeed> SanitizeDepartments(IReadOnlyList<AppSetupDepartmentConfigRequest>? departments)
    {
        if (departments is null || departments.Count == 0)
        {
            return [];
        }

        var sanitized = new List<AppSetupDepartmentSeed>(departments.Count);
        foreach (var department in departments)
        {
            var name = department.Name.Trim();
            var code = string.IsNullOrWhiteSpace(department.Code) ? name : department.Code.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            sanitized.Add(new AppSetupDepartmentSeed(
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

    private static IReadOnlyList<AppSetupPositionSeed> SanitizePositions(IReadOnlyList<AppSetupPositionConfigRequest>? positions)
    {
        if (positions is null || positions.Count == 0)
        {
            return [];
        }

        var sanitized = new List<AppSetupPositionSeed>(positions.Count);
        foreach (var position in positions)
        {
            var name = position.Name.Trim();
            var code = position.Code.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            sanitized.Add(new AppSetupPositionSeed(
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

    private TenantId ResolveTenantId()
    {
        var candidateValues = new[]
        {
            _configuration["Atlas:AppHost:TenantId"],
            _configuration["Security:BootstrapAdmin:TenantId"],
            DefaultTenantGuid.ToString("D")
        };

        foreach (var candidate in candidateValues)
        {
            if (!Guid.TryParse(candidate, out var tenantGuid))
            {
                continue;
            }

            return new TenantId(tenantGuid);
        }

        return new TenantId(DefaultTenantGuid);
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

    private static readonly HashSet<string> AllowedSetupRoleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SecurityAdmin",
        "AuditAdmin",
        "AssetAdmin",
        "ApprovalAdmin"
    };

    private static readonly IReadOnlyDictionary<string, AppSetupRoleTemplate> OptionalRoleTemplates =
        new Dictionary<string, AppSetupRoleTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["SecurityAdmin"] = new AppSetupRoleTemplate(
                "SecurityAdmin",
                "安全管理员",
                "负责应用安全策略与运行治理。",
                DataScopeType.All),
            ["AuditAdmin"] = new AppSetupRoleTemplate(
                "AuditAdmin",
                "审计管理员",
                "负责应用审计追踪与合规核查。",
                DataScopeType.All),
            ["AssetAdmin"] = new AppSetupRoleTemplate(
                "AssetAdmin",
                "资产管理员",
                "负责应用资产台账与生命周期管理。",
                DataScopeType.All),
            ["ApprovalAdmin"] = new AppSetupRoleTemplate(
                "ApprovalAdmin",
                "流程管理员",
                "负责应用审批流程配置与治理。",
                DataScopeType.All)
        };
}

public sealed record AppHostSetupStateResponse(
    string PlatformStatus,
    bool PlatformSetupCompleted,
    string AppStatus,
    bool AppSetupCompleted,
    string? AppKey);

public sealed record AppSetupInitializeResponse(
    string PlatformStatus,
    bool PlatformSetupCompleted,
    string AppStatus,
    bool AppSetupCompleted,
    bool DatabaseConnected,
    bool CoreTablesVerified,
    int RolesCreated,
    int DepartmentsCreated,
    int PositionsCreated,
    bool AdminBound,
    List<string> Errors,
    string? AppKey);

public sealed record AppSetupInitializeRequest(
    AppSetupDatabaseConfigRequest Database,
    AppSetupAdminConfigRequest Admin,
    AppSetupRoleConfigRequest? Roles,
    AppSetupOrganizationConfigRequest? Organization);

public sealed record AppSetupDatabaseConfigRequest(
    string DriverCode,
    string? Mode,
    string? ConnectionString,
    Dictionary<string, string>? VisualConfig);

public sealed record AppSetupAdminConfigRequest(
    string AppName,
    string AdminUsername,
    string? AppKey);

public sealed record AppSetupRoleConfigRequest(
    IReadOnlyList<string>? SelectedRoleCodes);

public sealed record AppSetupOrganizationConfigRequest(
    IReadOnlyList<AppSetupDepartmentConfigRequest>? Departments,
    IReadOnlyList<AppSetupPositionConfigRequest>? Positions);

public sealed record AppSetupDepartmentConfigRequest(
    string Name,
    string? Code,
    string? ParentCode,
    int SortOrder);

public sealed record AppSetupPositionConfigRequest(
    string Name,
    string Code,
    string? Description,
    int SortOrder);

public sealed record AppSetupTestConnectionRequest(
    AppSetupDatabaseConfigRequest Database);

public sealed record SetupTestConnectionResponse(
    bool Connected,
    string Message);

internal sealed record AppSetupRoleTemplate(
    string Code,
    string Name,
    string Description,
    DataScopeType DataScope);

internal sealed record AppSetupDepartmentSeed(
    string Name,
    string Code,
    string? ParentCode,
    int SortOrder);

internal sealed record AppSetupPositionSeed(
    string Name,
    string Code,
    string? Description,
    int SortOrder);

internal sealed record AppOrganizationSeedResult(
    int RolesCreated,
    int DepartmentsCreated,
    int PositionsCreated,
    bool AdminBound);
