using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Application.Identity;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Enums;
using Atlas.Core.Identity;
using Atlas.Core.Setup;
using Atlas.Core.Tenancy;
using Atlas.Domain.Alert.Entities;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AgentTeam.Entities;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Assets.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Domain.Plugins;
using Atlas.Domain.Events;
using Atlas.Domain.Workflow.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Setup;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BootstrapAdminOptions _bootstrapOptions;
    private readonly PasswordPolicyOptions _passwordPolicy;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly DatabaseInitializerOptions _initializerOptions;
    private readonly IHostEnvironment _environment;
    private readonly ISetupStateProvider _setupStateProvider;
    private readonly ISetupDbClientFactory _setupDbClientFactory;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    /// <summary>
    /// Setup 模式下由 SetupController 设置的显式参数。
    /// 为 null 时使用启动期冻结的 IOptions 值（正常启动态）。
    /// </summary>
    private SetupBootstrapParams? _activeBootstrapParams;

    public DatabaseInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BootstrapAdminOptions> bootstrapOptions,
        IOptions<PasswordPolicyOptions> passwordPolicy,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        IOptions<DatabaseInitializerOptions> initializerOptions,
        IHostEnvironment environment,
        ISetupStateProvider setupStateProvider,
        ISetupDbClientFactory setupDbClientFactory,
        ILogger<DatabaseInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _bootstrapOptions = bootstrapOptions.Value;
        _passwordPolicy = passwordPolicy.Value;
        _encryptionOptions = encryptionOptions.Value;
        _initializerOptions = initializerOptions.Value;
        _environment = environment;
        _setupStateProvider = setupStateProvider;
        _setupDbClientFactory = setupDbClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 供安装向导（SetupController）直接调用的初始化入口。
    /// 传入显式参数以绕过启动期冻结的 IOptions 和 ISqlSugarClient 门禁。
    /// </summary>
    public async Task<BootstrapReport> RunInitializationAsync(SetupBootstrapParams? bootstrapParams, CancellationToken cancellationToken)
    {
        _activeBootstrapParams = bootstrapParams;
        try
        {
            return await RunCoreAsync(cancellationToken);
        }
        finally
        {
            _activeBootstrapParams = null;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_setupStateProvider.IsReady)
        {
            _logger.LogInformation("[DatabaseInitializer] Setup 未完成，跳过自动数据库初始化（将由安装向导触发）");
            return;
        }

        await RunCoreAsync(cancellationToken);
    }

    private BootstrapAdminOptions GetEffectiveBootstrapOptions()
    {
        if (_activeBootstrapParams is null) return _bootstrapOptions;
        return new BootstrapAdminOptions
        {
            Enabled = true,
            TenantId = _activeBootstrapParams.TenantId,
            Username = _activeBootstrapParams.AdminUsername,
            Password = _activeBootstrapParams.AdminPassword,
            Roles = _activeBootstrapParams.AdminRoles,
            IsPlatformAdmin = _activeBootstrapParams.IsPlatformAdmin
        };
    }

    private DatabaseInitializerOptions GetEffectiveInitializerOptions()
    {
        if (_activeBootstrapParams is null) return _initializerOptions;
        return new DatabaseInitializerOptions
        {
            SkipSchemaInit = _activeBootstrapParams.SkipSchemaInit,
            SkipSeedData = _activeBootstrapParams.SkipSeedData,
            SkipSchemaMigrations = _activeBootstrapParams.SkipSchemaMigrations
        };
    }

    private async Task<BootstrapReport> RunCoreAsync(CancellationToken cancellationToken)
    {
        var report = new BootstrapReport();

        if (_encryptionOptions.Enabled && string.IsNullOrWhiteSpace(_encryptionOptions.Key))
        {
            _logger.LogWarning(
                "[DatabaseInitializer] Database:Encryption:Enabled=true 但 Key 未配置，" +
                "当前将自动降级为不加密运行。若为误配置，请检查环境变量 Database__Encryption__Enabled。");
        }

        using var scope = _scopeFactory.CreateScope();
        var appContextAccessor = scope.ServiceProvider.GetRequiredService<IAppContextAccessor>();

        // Setup 模式：使用 ISetupDbClientFactory 绕过 ISqlSugarClient 的 IsReady 门禁
        ISqlSugarClient db;
        ISqlSugarClient? setupOwnedDb = null;
        if (_activeBootstrapParams is not null)
        {
            setupOwnedDb = _setupDbClientFactory.Create(
                _activeBootstrapParams.ConnectionString,
                _activeBootstrapParams.DbType);
            db = setupOwnedDb;
            _logger.LogInformation("[DatabaseInitializer] 使用 setup 专用数据库连接");
        }
        else
        {
            db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        }

        try
        {
        var effectiveBootstrap = GetEffectiveBootstrapOptions();
        var effectiveInitializer = GetEffectiveInitializerOptions();

        // 关键兼容迁移：平台管理员标记字段缺失会导致登录链路失败，需始终检查并补齐。
        await EnsureUserAccountSchemaAsync(db, cancellationToken);
        await EnsureModelConfigSchemaAsync(db, cancellationToken);
        await EnsureAgentSchemaAsync(db, cancellationToken);
        await EnsureAiAppSchemaAsync(db, cancellationToken);
        var appMembershipAlignment = await EnsureAppMembershipSchemaAsync(db, cancellationToken);
        if (appMembershipAlignment is not null)
        {
            var repairedCount = appMembershipAlignment.RepairedTables.Count;
            if (repairedCount > 0)
            {
                _logger.LogWarning(
                    "[DatabaseInitializer] SQLite 应用域结构已自动清理/重建，repairedTables={RepairedCount}",
                    repairedCount);
            }

            foreach (var alignmentMessage in appMembershipAlignment.Messages)
            {
                _logger.LogInformation("[DatabaseInitializer][SqliteAlignment] {Message}", alignmentMessage);
            }
        }
        await EnsureAppPermissionSeedBackfillAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IIdGeneratorProvider>(),
            appContextAccessor.GetAppId(),
            cancellationToken);
        await EnsureSystemConfigSchemaAsync(db, cancellationToken);
        await EnsureAssetSchemaAsync(db, cancellationToken);
        // Schema 迁移检查（兼容历史版本字段结构）
        report.MigrationsApplied = !effectiveInitializer.SkipSchemaMigrations;
        if (!effectiveInitializer.SkipSchemaMigrations)
        {
            var migrationCount = 0;
            _logger.LogInformation("[DatabaseInitializer] 开始执行 Schema 迁移检查...");
            await EnsureAuthSessionSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureRefreshTokenSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureApprovalSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureTenantDataSourceSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureProductizationSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureWorkflowExecutionSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureAiPluginSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureWorkspacePortalSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureAiMemorySchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureAgentPublicationSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureTeamAgentSchemaAsync(db, cancellationToken); migrationCount++;
            await EnsureAgentTeamSchemaAsync(db, cancellationToken); migrationCount++;
            report.MigrationCount = migrationCount;
        }
        else
        {
            _logger.LogInformation("[DatabaseInitializer] 已跳过 Schema 迁移检查（DatabaseInitializer:SkipSchemaMigrations=true）");
        }

        // Schema 初始化（CodeFirst.InitTables）
        if (!effectiveInitializer.SkipSchemaInit)
        {
            _logger.LogInformation("[DatabaseInitializer] 开始执行 Schema 初始化（CodeFirst.InitTables）...");
            AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db);
            report.TablesCreated = AtlasOrmSchemaCatalog.RuntimeEntities.Count;
            report.SchemaInitialized = true;
        }
        else
        {
            _logger.LogInformation("[DatabaseInitializer] 已跳过 Schema 初始化（DatabaseInitializer:SkipSchemaInit=true）");
        }

        // 兼容历史库：Permission.AppId 误建为 NOT NULL 时，平台权限种子无法插入（AppId 应为 NULL）
        await EnsurePermissionAppIdNullableSchemaAsync(db, cancellationToken);

        await EnsureTenantAppDataSourceBindingBackfillAsync(scope.ServiceProvider, appContextAccessor, db, cancellationToken);
        await EnsureTenantAppDataSourceBindingHealthAsync(scope.ServiceProvider, appContextAccessor, db, cancellationToken);
        await EnsureOrphanAppProvisioningAsync(scope.ServiceProvider, appContextAccessor, db, cancellationToken);
        await EnsureDefaultWorkspacesAsync(scope.ServiceProvider, db, cancellationToken);
        await EnsureTeamAgentTemplateSeedDataAsync(
            db,
            appContextAccessor,
            scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>(),
            cancellationToken);

        // 种子数据初始化
        if (effectiveInitializer.SkipSeedData)
        {
            var menuCount = await db.Queryable<Menu>().CountAsync(cancellationToken);
            if (menuCount == 0)
            {
                _logger.LogWarning(
                    "[DatabaseInitializer] 检测到 Menu 表为空（新环境或重建 DB），" +
                    "自动忽略 SkipSeedData=true 配置，执行首次种子数据播种。");
            }
            else
            {
                _logger.LogInformation("[DatabaseInitializer] 已跳过种子数据初始化（DatabaseInitializer:SkipSeedData=true）");
                var adminPermissionCheck = await EnsureBootstrapAdminMaxPermissionsAsync(
                    scope,
                    appContextAccessor,
                    db,
                    effectiveBootstrap,
                    cancellationToken);
                ApplyAdminPermissionCheckReport(report, adminPermissionCheck);
                await LoadLicenseStatusAsync(scope, cancellationToken);
                await EnsureDefaultWorkspacesAsync(scope.ServiceProvider, db, cancellationToken);
                return report;
            }
        }

        _logger.LogInformation("[DatabaseInitializer] 开始执行种子数据初始化...");

        if (Guid.TryParse(effectiveBootstrap.TenantId, out var seedTenantGuid))
        {
            var approvalSeedService = scope.ServiceProvider.GetRequiredService<ApprovalSeedDataService>();
            var templateSeedDataService = scope.ServiceProvider.GetRequiredService<TemplateSeedDataService>();
            var seedTenantId = new TenantId(seedTenantGuid);
            using var seedScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, seedTenantId));
            await approvalSeedService.InitializeSeedDataAsync(seedTenantId, cancellationToken);
            await templateSeedDataService.InitializeBuiltInTemplatesAsync(seedTenantId, cancellationToken);
        }

        if (Guid.TryParse(effectiveBootstrap.TenantId, out var appTenantGuid))
        {
            var appTenantId = new TenantId(appTenantGuid);
            using var appConfigScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, appTenantId));
            var appConfigRepository = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var appConfigIdGenerator = scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();
            var appId = appContextAccessor.GetAppId();
            var existingAppConfig = await appConfigRepository.FindByAppIdAsync(appTenantId, appId, cancellationToken);
            if (existingAppConfig is null)
            {
                var appConfig = new AppConfig(appTenantId, appId, appId, appConfigIdGenerator.NextId());
                appConfig.Update(appId, true, false, "默认应用配置", 0);
                await appConfigRepository.AddAsync(appConfig, cancellationToken);
            }
        }

        if (Guid.TryParse(effectiveBootstrap.TenantId, out var configTenantGuid))
        {
            var configTenantId = new TenantId(configTenantGuid);
            using var configScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, configTenantId));
            await EnsureBuiltInSystemConfigsAsync(
                scope.ServiceProvider.GetRequiredService<SystemConfigRepository>(),
                scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>(),
                configTenantId,
                cancellationToken);
        }

        report.SeedCompleted = true;
        report.SeedSummary =
            $"系统元数据已播种，新增角色 {report.RolesCreated} 个、部门 {report.DepartmentsCreated} 个、岗位 {report.PositionsCreated} 个。";

        await LoadLicenseStatusAsync(scope, cancellationToken);

        if (!effectiveBootstrap.Enabled)
        {
            ApplyAdminPermissionCheckReport(
                report,
                BootstrapAdminPermissionCheckResult.Success([], "BootstrapAdmin 已禁用，跳过超管权限一致性校验。"));
            return report;
        }

        if (string.IsNullOrWhiteSpace(effectiveBootstrap.Password))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("未配置BootstrapAdmin密码，已跳过创建默认管理员。");
                var adminPermissionCheck = await EnsureBootstrapAdminMaxPermissionsAsync(
                    scope,
                    appContextAccessor,
                    db,
                    effectiveBootstrap,
                    cancellationToken);
                ApplyAdminPermissionCheckReport(report, adminPermissionCheck);
                return report;
            }

            throw new InvalidOperationException("生产环境必须配置BootstrapAdmin密码。");
        }

        if (!PasswordPolicy.IsCompliant(effectiveBootstrap.Password, _passwordPolicy, out var message))
        {
            throw new InvalidOperationException($"BootstrapAdmin密码不符合策略：{message}");
        }

        if (!Guid.TryParse(effectiveBootstrap.TenantId, out var tenantGuid))
        {
            throw new InvalidOperationException("BootstrapAdmin TenantId格式错误。");
        }

        var tenantId = new TenantId(tenantGuid);
        using var appContextScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, tenantId));
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var idGeneratorAccessor = scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var userRoleRepository = scope.ServiceProvider.GetRequiredService<IUserRoleRepository>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var rolePermissionRepository = scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>();
        var roleMenuRepository = scope.ServiceProvider.GetRequiredService<IRoleMenuRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var requiredRoleDefinitions = new (string Code, string Name, string Description, bool IsSystem)[]
        {
            ("SuperAdmin", "超级管理员", "平台超级管理员（全量权限）", true),
            ("Admin", "系统管理员", "系统运维与平台配置管理员", true)
        };
        var optionalRoleDefinitions = new (string Code, string Name, string Description, bool IsSystem)[]
        {
            ("SecurityAdmin", "安全管理员", "安全策略与告警管理员", false),
            ("AuditAdmin", "审计管理员", "审计日志与合规管理员", false),
            ("AssetAdmin", "资产管理员", "资产台账管理员", false),
            ("ApprovalAdmin", "流程管理员", "审批流配置管理员", false)
        };

        var roleSeedMap = requiredRoleDefinitions
            .Concat(optionalRoleDefinitions)
            .ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
        var roleCodes = effectiveBootstrap.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!roleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
        {
            roleCodes.Add("SuperAdmin");
        }
        var roleCodesArray = roleCodes.ToArray();

        var roleCodeSet = roleCodesArray.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allRoleCodes = requiredRoleDefinitions
            .Select(x => x.Code)
            .Concat(roleCodesArray)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingRoles = await roleRepository.QueryByCodesAsync(tenantId, allRoleCodes, cancellationToken);
        var roleMap = existingRoles.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var rolesToInsert = new List<Role>();
        foreach (var roleCode in allRoleCodes)
        {
            if (roleMap.ContainsKey(roleCode))
            {
                continue;
            }

            var displayName = roleSeedMap.TryGetValue(roleCode, out var seed) ? seed.Name : roleCode;
            var description = roleSeedMap.TryGetValue(roleCode, out seed) ? seed.Description : roleCode;
            var role = new Role(tenantId, displayName, roleCode, idGeneratorAccessor.NextId());
            role.Update(displayName, description);
            if (string.Equals(roleCode, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                role.SetDataScope(DataScopeType.All);
            }

            if (roleCodeSet.Contains(roleCode) || (roleSeedMap.TryGetValue(roleCode, out seed) && seed.IsSystem))
            {
                role.MarkSystemRole();
            }
            rolesToInsert.Add(role);
            roleMap[roleCode] = role;
        }

        if (rolesToInsert.Count > 0)
        {
            await roleRepository.AddRangeAsync(rolesToInsert, cancellationToken);
        }
        report.RolesCreated = rolesToInsert.Count;

        await EnsureBootstrapRoleDataScopesAsync(roleRepository, roleMap, allRoleCodes, cancellationToken);

        var roleIds = roleCodesArray
            .Select(roleCode => roleMap[roleCode].Id)
            .Distinct()
            .ToList();

        var permissionSeeds = new (string Code, string Name, string Type)[]
        {
            (PermissionCodes.SystemAdmin, "System Admin", "Api"),
            (PermissionCodes.WorkflowDesign, "Workflow Designer", "Menu"),
            (PermissionCodes.WorkflowView, "Workflow View", "Api"),
            (PermissionCodes.UsersView, "Users View", "Api"),
            (PermissionCodes.UsersCreate, "Users Create", "Api"),
            (PermissionCodes.UsersUpdate, "Users Update", "Api"),
            (PermissionCodes.UsersDelete, "Users Delete", "Api"),
            (PermissionCodes.UsersAssignRoles, "Users Assign Roles", "Api"),
            (PermissionCodes.UsersAssignDepartments, "Users Assign Departments", "Api"),
            (PermissionCodes.UsersAssignPositions, "Users Assign Positions", "Api"),
            (PermissionCodes.RolesView, "Roles View", "Api"),
            (PermissionCodes.RolesCreate, "Roles Create", "Api"),
            (PermissionCodes.RolesUpdate, "Roles Update", "Api"),
            (PermissionCodes.RolesDelete, "Roles Delete", "Api"),
            (PermissionCodes.RolesAssignPermissions, "Roles Assign Permissions", "Api"),
            (PermissionCodes.RolesAssignMenus, "Roles Assign Menus", "Api"),
            (PermissionCodes.PermissionsView, "Permissions View", "Api"),
            (PermissionCodes.PermissionsCreate, "Permissions Create", "Api"),
            (PermissionCodes.PermissionsUpdate, "Permissions Update", "Api"),
            (PermissionCodes.DepartmentsView, "Departments View", "Api"),
            (PermissionCodes.DepartmentsAll, "Departments All", "Api"),
            (PermissionCodes.DepartmentsCreate, "Departments Create", "Api"),
            (PermissionCodes.DepartmentsUpdate, "Departments Update", "Api"),
            (PermissionCodes.DepartmentsDelete, "Departments Delete", "Api"),
            (PermissionCodes.PositionsView, "Positions View", "Api"),
            (PermissionCodes.PositionsCreate, "Positions Create", "Api"),
            (PermissionCodes.PositionsUpdate, "Positions Update", "Api"),
            (PermissionCodes.PositionsDelete, "Positions Delete", "Api"),
            (PermissionCodes.MenusView, "Menus View", "Api"),
            (PermissionCodes.MenusAll, "Menus All", "Api"),
            (PermissionCodes.MenusCreate, "Menus Create", "Api"),
            (PermissionCodes.MenusUpdate, "Menus Update", "Api"),
            (PermissionCodes.MenusDelete, "Menus Delete", "Api"),
            (PermissionCodes.AppsView, "Apps View", "Api"),
            (PermissionCodes.AppsUpdate, "Apps Update", "Api"),
            (PermissionCodes.AppMembersView, "App Members View", "Api"),
            (PermissionCodes.AppMembersUpdate, "App Members Update", "Api"),
            (PermissionCodes.AppRolesView, "App Roles View", "Api"),
            (PermissionCodes.AppRolesUpdate, "App Roles Update", "Api"),
            (PermissionCodes.AppAdmin, "App Admin", "Api"),
            (PermissionCodes.AppUser, "App User", "Api"),
            (PermissionCodes.DebugView, "Debug View", "Api"),
            (PermissionCodes.DebugRun, "Debug Run", "Api"),
            (PermissionCodes.DebugManage, "Debug Manage", "Api"),
            (PermissionCodes.ProjectsView, "Projects View", "Api"),
            (PermissionCodes.ProjectsCreate, "Projects Create", "Api"),
            (PermissionCodes.ProjectsUpdate, "Projects Update", "Api"),
            (PermissionCodes.ProjectsDelete, "Projects Delete", "Api"),
            (PermissionCodes.ProjectsAssignUsers, "Projects Assign Users", "Api"),
            (PermissionCodes.ProjectsAssignDepartments, "Projects Assign Departments", "Api"),
            (PermissionCodes.ProjectsAssignPositions, "Projects Assign Positions", "Api"),
            (PermissionCodes.AuditView, "Audit View", "Api"),
            (PermissionCodes.AssetsView, "Assets View", "Api"),
            (PermissionCodes.AssetsCreate, "Assets Create", "Api"),
            (PermissionCodes.AlertView, "Alert View", "Api"),
            (PermissionCodes.ModelConfigView, "Model Config View", "Api"),
            (PermissionCodes.ModelConfigCreate, "Model Config Create", "Api"),
            (PermissionCodes.ModelConfigUpdate, "Model Config Update", "Api"),
            (PermissionCodes.ModelConfigDelete, "Model Config Delete", "Api"),
            (PermissionCodes.AgentView, "Agent View", "Api"),
            (PermissionCodes.AgentCreate, "Agent Create", "Api"),
            (PermissionCodes.AgentUpdate, "Agent Update", "Api"),
            (PermissionCodes.AgentDelete, "Agent Delete", "Api"),
            (PermissionCodes.ConversationView, "Conversation View", "Api"),
            (PermissionCodes.ConversationCreate, "Conversation Create", "Api"),
            (PermissionCodes.ConversationDelete, "Conversation Delete", "Api"),
            (PermissionCodes.KnowledgeBaseView, "Knowledge Base View", "Api"),
            (PermissionCodes.KnowledgeBaseCreate, "Knowledge Base Create", "Api"),
            (PermissionCodes.KnowledgeBaseUpdate, "Knowledge Base Update", "Api"),
            (PermissionCodes.KnowledgeBaseDelete, "Knowledge Base Delete", "Api"),
            (PermissionCodes.AiWorkflowView, "AI Workflow View", "Api"),
            (PermissionCodes.AiWorkflowCreate, "AI Workflow Create", "Api"),
            (PermissionCodes.AiWorkflowUpdate, "AI Workflow Update", "Api"),
            (PermissionCodes.AiWorkflowDelete, "AI Workflow Delete", "Api"),
            (PermissionCodes.AiWorkflowExecute, "AI Workflow Execute", "Api"),
            (PermissionCodes.AiWorkflowDebug, "AI Workflow Debug", "Api"),
            (PermissionCodes.AiDatabaseView, "AI Database View", "Api"),
            (PermissionCodes.AiDatabaseCreate, "AI Database Create", "Api"),
            (PermissionCodes.AiDatabaseUpdate, "AI Database Update", "Api"),
            (PermissionCodes.AiDatabaseDelete, "AI Database Delete", "Api"),
            (PermissionCodes.AiVariableView, "AI Variable View", "Api"),
            (PermissionCodes.AiVariableCreate, "AI Variable Create", "Api"),
            (PermissionCodes.AiVariableUpdate, "AI Variable Update", "Api"),
            (PermissionCodes.AiVariableDelete, "AI Variable Delete", "Api"),
            (PermissionCodes.AiPluginView, "AI Plugin View", "Api"),
            (PermissionCodes.AiPluginCreate, "AI Plugin Create", "Api"),
            (PermissionCodes.AiPluginUpdate, "AI Plugin Update", "Api"),
            (PermissionCodes.AiPluginDelete, "AI Plugin Delete", "Api"),
            (PermissionCodes.AiPluginPublish, "AI Plugin Publish", "Api"),
            (PermissionCodes.AiPluginDebug, "AI Plugin Debug", "Api"),
            (PermissionCodes.AiAppView, "AI App View", "Api"),
            (PermissionCodes.AiAppCreate, "AI App Create", "Api"),
            (PermissionCodes.AiAppUpdate, "AI App Update", "Api"),
            (PermissionCodes.AiAppDelete, "AI App Delete", "Api"),
            (PermissionCodes.AiAppPublish, "AI App Publish", "Api"),
            (PermissionCodes.AiPromptView, "AI Prompt View", "Api"),
            (PermissionCodes.AiPromptCreate, "AI Prompt Create", "Api"),
            (PermissionCodes.AiPromptUpdate, "AI Prompt Update", "Api"),
            (PermissionCodes.AiPromptDelete, "AI Prompt Delete", "Api"),
            (PermissionCodes.AiMarketplaceView, "AI Marketplace View", "Api"),
            (PermissionCodes.AiMarketplaceCreate, "AI Marketplace Create", "Api"),
            (PermissionCodes.AiMarketplaceUpdate, "AI Marketplace Update", "Api"),
            (PermissionCodes.AiMarketplaceDelete, "AI Marketplace Delete", "Api"),
            (PermissionCodes.AiMarketplacePublish, "AI Marketplace Publish", "Api"),
            (PermissionCodes.AiSearchView, "AI Search View", "Api"),
            (PermissionCodes.AiSearchUpdate, "AI Search Update", "Api"),
            (PermissionCodes.AiAdminConfigView, "AI Admin Config View", "Api"),
            (PermissionCodes.AiAdminConfigUpdate, "AI Admin Config Update", "Api"),
            (PermissionCodes.AiWorkspaceView, "AI Workspace View", "Api"),
            (PermissionCodes.AiWorkspaceUpdate, "AI Workspace Update", "Api"),
            (PermissionCodes.AiDevopsView, "AI DevOps View", "Api"),
            (PermissionCodes.AiShortcutView, "AI Shortcut View", "Api"),
            (PermissionCodes.AiShortcutManage, "AI Shortcut Manage", "Api"),
            (PermissionCodes.PersonalAccessTokenView, "PAT View", "Api"),
            (PermissionCodes.PersonalAccessTokenCreate, "PAT Create", "Api"),
            (PermissionCodes.PersonalAccessTokenUpdate, "PAT Update", "Api"),
            (PermissionCodes.PersonalAccessTokenDelete, "PAT Delete", "Api"),
            (PermissionCodes.ApprovalFlowView, "Approval Flow View", "Api"),
            (PermissionCodes.ApprovalFlowManage, "Approval Flow Manage", "Api"),
            (PermissionCodes.ApprovalFlowCreate, "Approval Flow Create", "Api"),
            (PermissionCodes.ApprovalFlowUpdate, "Approval Flow Update", "Api"),
            (PermissionCodes.ApprovalFlowPublish, "Approval Flow Publish", "Api"),
            (PermissionCodes.ApprovalFlowDelete, "Approval Flow Delete", "Api"),
            (PermissionCodes.ApprovalFlowDisable, "Approval Flow Disable", "Api"),
            (PermissionCodes.VisualizationView, "Visualization View", "Api"),
            (PermissionCodes.VisualizationProcessSave, "Visualization Process Save", "Api"),
            (PermissionCodes.VisualizationProcessUpdate, "Visualization Process Update", "Api"),
            (PermissionCodes.VisualizationProcessPublish, "Visualization Process Publish", "Api"),
            (PermissionCodes.DictTypeView, "Dict Type View", "Api"),
            (PermissionCodes.DictTypeCreate, "Dict Type Create", "Api"),
            (PermissionCodes.DictTypeUpdate, "Dict Type Update", "Api"),
            (PermissionCodes.DictTypeDelete, "Dict Type Delete", "Api"),
            (PermissionCodes.DictDataView, "Dict Data View", "Api"),
            (PermissionCodes.DictDataCreate, "Dict Data Create", "Api"),
            (PermissionCodes.DictDataUpdate, "Dict Data Update", "Api"),
            (PermissionCodes.DictDataDelete, "Dict Data Delete", "Api"),
            (PermissionCodes.ConfigView, "Config View", "Api"),
            (PermissionCodes.ConfigCreate, "Config Create", "Api"),
            (PermissionCodes.ConfigUpdate, "Config Update", "Api"),
            (PermissionCodes.ConfigDelete, "Config Delete", "Api"),
            (PermissionCodes.LoginLogView, "Login Log View", "Api"),
            (PermissionCodes.LoginLogDelete, "Login Log Delete", "Api"),
            (PermissionCodes.OnlineUsersView, "Online Users View", "Api"),
            (PermissionCodes.OnlineUsersForceLogout, "Online Users Force Logout", "Api"),
            (PermissionCodes.MonitorView, "Monitor View", "Api"),
            (PermissionCodes.NotificationView, "Notification View", "Api"),
            (PermissionCodes.NotificationCreate, "Notification Create", "Api"),
            (PermissionCodes.NotificationUpdate, "Notification Update", "Api"),
            (PermissionCodes.NotificationDelete, "Notification Delete", "Api"),
            (PermissionCodes.JobView, "Job View", "Api"),
            (PermissionCodes.JobCreate, "Job Create", "Api"),
            (PermissionCodes.JobUpdate, "Job Update", "Api"),
            (PermissionCodes.JobDelete, "Job Delete", "Api"),
            (PermissionCodes.JobTrigger, "Job Trigger", "Api"),
            (PermissionCodes.DataScopeManage, "Data Scope Manage", "Api"),
            (PermissionCodes.ToolPoliciesView, "Tool Policies View", "Api"),
            (PermissionCodes.WebhooksView, "Webhooks View", "Api"),
            (PermissionCodes.WebhooksCreate, "Webhooks Create", "Api"),
            (PermissionCodes.WebhooksUpdate, "Webhooks Update", "Api"),
            (PermissionCodes.WebhooksDelete, "Webhooks Delete", "Api"),
            (PermissionCodes.WebhooksTest, "Webhooks Test", "Api"),
            (PermissionCodes.TemplatesView, "Templates View", "Api"),
            (PermissionCodes.TemplatesCreate, "Templates Create", "Api"),
            (PermissionCodes.TemplatesUpdate, "Templates Update", "Api"),
            (PermissionCodes.TemplatesDelete, "Templates Delete", "Api"),
            (PermissionCodes.TemplatesInstantiate, "Templates Instantiate", "Api"),
            (PermissionCodes.ConnectorsView, "Connectors View", "Api"),
            (PermissionCodes.ConnectorsCreate, "Connectors Create", "Api"),
            (PermissionCodes.ConnectorsUpdate, "Connectors Update", "Api"),
            (PermissionCodes.ConnectorsDelete, "Connectors Delete", "Api"),
            (PermissionCodes.ConnectorsSync, "Connectors Sync", "Api"),
            (PermissionCodes.ConnectorsExecute, "Connectors Execute", "Api"),
            (PermissionCodes.PlatformEventsView, "Platform Events View", "Api"),
            (PermissionCodes.PackagesExport, "Packages Export", "Api"),
            (PermissionCodes.PackagesImport, "Packages Import", "Api"),
            (PermissionCodes.PackagesAnalyze, "Packages Analyze", "Api"),
            (PermissionCodes.AlertRulesView, "Alert Rules View", "Api"),
            (PermissionCodes.AlertRulesCreate, "Alert Rules Create", "Api"),
            (PermissionCodes.AlertRulesUpdate, "Alert Rules Update", "Api"),
            (PermissionCodes.AlertRulesDelete, "Alert Rules Delete", "Api"),
            (PermissionCodes.MeteringView, "Metering View", "Api"),
            (PermissionCodes.MeteringUpdate, "Metering Update", "Api"),
            (PermissionCodes.LicenseView, "License View", "Api"),
            (PermissionCodes.LicenseManage, "License Manage", "Api"),
            (PermissionCodes.FileUpload, "File Upload", "Api"),
            (PermissionCodes.FileDownload, "File Download", "Api"),
            (PermissionCodes.FileDelete, "File Delete", "Api")
        };

        var permissionIdMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        Permission? adminPermission = null;
        Permission? workflowPermission = null;

        var permissionCodes = permissionSeeds
            .Select(x => x.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingPermissions = await db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(permissionCodes, x.Code))
            .ToListAsync(cancellationToken);
        var permissionMap = existingPermissions.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var permissionsToInsert = new List<Permission>();
        foreach (var seed in permissionSeeds)
        {
            if (permissionMap.ContainsKey(seed.Code))
            {
                continue;
            }

            var permission = new Permission(tenantId, seed.Name, seed.Code, seed.Type, idGeneratorAccessor.NextId());
            permissionsToInsert.Add(permission);
            permissionMap[seed.Code] = permission;
        }

        if (permissionsToInsert.Count > 0)
        {
            await db.Insertable(permissionsToInsert).ExecuteCommandAsync(cancellationToken);
        }

        foreach (var permission in permissionMap.Values)
        {
            permissionIdMap[permission.Code] = permission.Id;
            if (string.Equals(permission.Code, PermissionCodes.SystemAdmin, StringComparison.OrdinalIgnoreCase))
            {
                adminPermission = permission;
            }

            if (string.Equals(permission.Code, PermissionCodes.WorkflowDesign, StringComparison.OrdinalIgnoreCase))
            {
                workflowPermission = permission;
            }
        }

        if (adminPermission is null || workflowPermission is null)
        {
            throw new InvalidOperationException("初始化权限失败：缺少 system:admin 或 workflow:design。");
        }

        var menuSeeds = new (
            string Name,
            string Path,
            string? ParentPath,
            int SortOrder,
            string MenuType,
            string? Component,
            string? Icon,
            string? Perms,
            string? Query,
            bool IsFrame,
            bool IsCache,
            string Visible,
            string Status,
            string? PermissionCode,
            bool IsHidden)[]
        {
            ("首页", "/", null, 0, "C", "home", "dashboard", PermissionCodes.SystemAdmin, null, false, false, "0", "0", PermissionCodes.SystemAdmin, false),

            ("平台控制台", "/console", null, 5, "M", "Layout", "dashboard", null, null, false, false, "0", "0", null, false),
            ("控制台首页", "/console/apps", "/console", 6, "C", "console/ConsolePage", "appstore", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("控制台数据源", "/console/datasources", "/console", 7, "C", "system/TenantDataSourcesPage", "database", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("控制台系统设置", "/console/settings/system/configs", "/console", 8, "C", "system/SystemConfigsPage", "tool", PermissionCodes.ConfigView, null, false, true, "0", "0", PermissionCodes.ConfigView, false),

            ("安全中心", "/security", null, 10, "M", "Layout", "safety-certificate", null, null, false, false, "0", "0", null, false),
            ("资产管理", "/assets", "/security", 11, "C", "AssetsPage", "database", PermissionCodes.AssetsView, null, false, true, "0", "0", PermissionCodes.AssetsView, false),
            ("审计日志", "/audit", "/security", 12, "C", "AuditPage", "file-search", PermissionCodes.AuditView, null, false, true, "0", "0", PermissionCodes.AuditView, false),
            ("告警管理", "/alert", "/security", 13, "C", "AlertPage", "bell", PermissionCodes.AlertView, null, false, true, "0", "0", PermissionCodes.AlertView, false),
            ("可视化中心", "/visualization", "/security", 14, "C", "visualization/VisualizationCenterPage", "fund", PermissionCodes.VisualizationView, null, false, true, "0", "0", PermissionCodes.VisualizationView, false),
            ("可视化设计", "/visualization/designer", "/security", 15, "C", "visualization/VisualizationDesignerPage", "deployment-unit", PermissionCodes.VisualizationProcessSave, null, false, true, "0", "0", PermissionCodes.VisualizationProcessSave, true),
            ("可视化运行", "/visualization/runtime", "/security", 16, "C", "visualization/VisualizationRuntimePage", "play-circle", PermissionCodes.VisualizationView, null, false, true, "0", "0", PermissionCodes.VisualizationView, false),
            ("可视化治理", "/visualization/governance", "/security", 17, "C", "visualization/VisualizationGovernancePage", "audit", PermissionCodes.AuditView, null, false, true, "0", "0", PermissionCodes.AuditView, false),
            ("AI 平台", "/ai", null, 14, "M", "Layout", "robot", null, null, false, false, "0", "0", null, false),
            ("模型配置", "/settings/ai/model-configs", "/ai", 15, "C", "ai/ModelConfigsPage", "api", PermissionCodes.ModelConfigView, null, false, true, "0", "0", PermissionCodes.ModelConfigView, false),
            ("Agent 管理", "/ai/agents", "/ai", 16, "C", "ai/AgentListPage", "experiment", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, false),
            ("Agent 编辑", "/ai/agents/:id/edit", "/ai", 17, "C", "ai/AgentEditorPage", "edit", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, true),
            ("知识库管理", "/ai/knowledge-bases", "/ai", 18, "C", "ai/KnowledgeBaseListPage", "book", PermissionCodes.KnowledgeBaseView, null, false, true, "0", "0", PermissionCodes.KnowledgeBaseView, false),
            ("知识库详情", "/ai/knowledge-bases/:id", "/ai", 19, "C", "ai/KnowledgeBaseDetailPage", "read", PermissionCodes.KnowledgeBaseView, null, false, true, "0", "0", PermissionCodes.KnowledgeBaseView, true),
            ("记忆管理", "/ai/memories", "/ai", 20, "C", "ai/UserMemorySettingsPage", "history", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, false),
            ("数据库管理", "/ai/databases", "/ai", 20, "C", "ai/AiDatabaseListPage", "database", PermissionCodes.AiDatabaseView, null, false, true, "0", "0", PermissionCodes.AiDatabaseView, false),
            ("数据库详情", "/ai/databases/:id", "/ai", 21, "C", "ai/AiDatabaseDetailPage", "table", PermissionCodes.AiDatabaseView, null, false, true, "0", "0", PermissionCodes.AiDatabaseView, true),
            ("变量管理", "/ai/variables", "/ai", 22, "C", "ai/AiVariablesPage", "code", PermissionCodes.AiVariableView, null, false, true, "0", "0", PermissionCodes.AiVariableView, false),
            ("插件管理", "/ai/plugins", "/ai", 23, "C", "ai/AiPluginListPage", "api", PermissionCodes.AiPluginView, null, false, true, "0", "0", PermissionCodes.AiPluginView, false),
            ("插件详情", "/ai/plugins/:id", "/ai", 24, "C", "ai/AiPluginDetailPage", "setting", PermissionCodes.AiPluginView, null, false, true, "0", "0", PermissionCodes.AiPluginView, true),
            ("应用管理", "/ai/apps", "/ai", 25, "C", "ai/AiAppListPage", "appstore", PermissionCodes.AiAppView, null, false, true, "0", "0", PermissionCodes.AiAppView, false),
            ("应用编辑", "/ai/apps/:id/edit", "/ai", 26, "C", "ai/AiAppEditorPage", "edit", PermissionCodes.AiAppView, null, false, true, "0", "0", PermissionCodes.AiAppView, true),
            ("Prompt 模板", "/ai/prompts", "/ai", 27, "C", "ai/AiPromptLibraryPage", "snippets", PermissionCodes.AiPromptView, null, false, true, "0", "0", PermissionCodes.AiPromptView, false),
            ("开放平台", "/ai/open-platform", "/ai", 28, "C", "ai/AiOpenPlatformPage", "cloud", PermissionCodes.PersonalAccessTokenView, null, false, true, "0", "0", PermissionCodes.PersonalAccessTokenView, false),
            ("AI 市场", "/ai/marketplace", "/ai", 29, "C", "ai/AiMarketplacePage", "shop", PermissionCodes.AiMarketplaceView, null, false, true, "0", "0", PermissionCodes.AiMarketplaceView, false),
            ("AI 市场详情", "/ai/marketplace/:id", "/ai", 30, "C", "ai/AiMarketplaceDetailPage", "profile", PermissionCodes.AiMarketplaceView, null, false, true, "0", "0", PermissionCodes.AiMarketplaceView, true),
            ("统一搜索", "/ai/search", "/ai", 31, "C", "ai/AiSearchResultsPage", "search", PermissionCodes.AiSearchView, null, false, true, "0", "0", PermissionCodes.AiSearchView, false),
            ("AI 工作台", "/ai/workspace", "/ai", 32, "C", "ai/AiWorkspacePage", "desktop", PermissionCodes.AiWorkspaceView, null, false, true, "0", "0", PermissionCodes.AiWorkspaceView, false),
            ("AI 资源库", "/ai/library", "/ai", 33, "C", "ai/AiLibraryPage", "inbox", PermissionCodes.AiWorkspaceView, null, false, true, "0", "0", PermissionCodes.AiWorkspaceView, false),
            ("AI 测试集", "/ai/devops/test-sets", "/ai", 34, "C", "ai/AiTestSetsPage", "experiment", PermissionCodes.AiDevopsView, null, false, true, "0", "0", PermissionCodes.AiDevopsView, false),
            ("AI Mock 集", "/ai/devops/mock-sets", "/ai", 35, "C", "ai/AiMockSetsPage", "api", PermissionCodes.AiDevopsView, null, false, true, "0", "0", PermissionCodes.AiDevopsView, false),
            ("快捷命令", "/ai/shortcuts", "/ai", 36, "C", "ai/AiShortcutsPage", "thunderbolt", PermissionCodes.AiShortcutView, null, false, true, "0", "0", PermissionCodes.AiShortcutView, false),

            ("流程中心", "/process", null, 25, "M", "Layout", "cluster", null, null, false, false, "0", "0", null, false),
            ("流程定义", "/approval/flows", "/process", 26, "C", "ApprovalFlowsPage", "apartment", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("发起审批", "/process/start", "/process", 27, "C", "ApprovalStartPage", "send", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的待办", "/process/inbox", "/process", 28, "C", "ApprovalInboxPage", "schedule", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的已办", "/process/done", "/process", 29, "C", "ApprovalDonePage", "check-circle", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我发起的", "/process/my-requests", "/process", 30, "C", "ApprovalMyRequestsPage", "profile", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的抄送", "/process/cc", "/process", 31, "C", "ApprovalCcPage", "mail", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("审批设计器", "/process/designer", "/process", 32, "C", "ApprovalDesignerPage", "branches", PermissionCodes.ApprovalFlowUpdate, null, false, true, "0", "0", PermissionCodes.ApprovalFlowUpdate, true),
            ("审批设计详情", "/process/designer/:id", "/process", 33, "C", "ApprovalDesignerPage", "branches", PermissionCodes.ApprovalFlowUpdate, null, false, true, "0", "0", PermissionCodes.ApprovalFlowUpdate, true),
            ("审批任务详情", "/process/tasks/:id", "/process", 34, "C", "ApprovalTaskDetailPage", "file-search", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, true),
            ("审批实例详情", "/process/instances/:id", "/process", 35, "C", "ApprovalInstanceDetailPage", "file-text", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, true),
            ("流程定义管理", "/process/manage/flows", "/process", 36, "C", "ApprovalFlowManagePage", "control", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("流程实例管理", "/process/manage/instances", "/process", 37, "C", "ApprovalInstanceManagePage", "appstore", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("审批代理配置", "/process/agent-config", "/process", 38, "C", "ApprovalAgentConfigPage", "user-switch", PermissionCodes.ApprovalFlowManage, null, false, true, "0", "0", PermissionCodes.ApprovalFlowManage, false),
            ("部门负责人配置", "/process/department-leaders", "/process", 39, "C", "ApprovalDepartmentLeaderPage", "team", PermissionCodes.ApprovalFlowManage, null, false, true, "0", "0", PermissionCodes.ApprovalFlowManage, false),
            ("任务池", "/process/task-pool", "/process", 40, "C", "ApprovalTaskPoolPage", "inbox", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("工作流设计", "/workflow/designer", "/process", 41, "C", "WorkflowDesignerPage", "branches", workflowPermission.Code, null, false, true, "0", "0", workflowPermission.Code, false),

            ("运维监控", "/monitor", null, 35, "M", "Layout", "monitor", null, null, false, false, "0", "0", null, false),
            ("服务器监控", "/monitor/server-info", "/monitor", 37, "C", "monitor/ServerInfoPage", "desktop", PermissionCodes.MonitorView, null, false, true, "0", "0", PermissionCodes.MonitorView, false),
            ("定时任务", "/monitor/scheduled-jobs", "/monitor", 38, "C", "monitor/ScheduledJobsPage", "clock-circle", PermissionCodes.JobView, null, false, true, "0", "0", PermissionCodes.JobView, false),
            ("登录日志", "/system/login-logs", "/monitor", 39, "C", "system/LoginLogsPage", "file-search", PermissionCodes.LoginLogView, null, false, true, "0", "0", PermissionCodes.LoginLogView, false),
            ("在线用户", "/system/online-users", "/monitor", 40, "C", "system/OnlineUsersPage", "user", PermissionCodes.OnlineUsersView, null, false, true, "0", "0", PermissionCodes.OnlineUsersView, false),

            ("系统管理", "/system", null, 30, "M", "Layout", "setting", null, null, false, false, "0", "0", null, false),
            ("组织架构", "/settings/org/departments", "/system", 31, "C", "system/DepartmentsPage", "cluster", PermissionCodes.DepartmentsView, null, false, true, "0", "0", PermissionCodes.DepartmentsView, false),
            ("租户管理", "/settings/org/tenants", "/system", 32, "C", "system/TenantsPage", "global", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("职位名称", "/settings/org/positions", "/system", 33, "C", "system/PositionsPage", "idcard", PermissionCodes.PositionsView, null, false, true, "0", "0", PermissionCodes.PositionsView, false),
            ("员工管理", "/settings/org/users", "/system", 34, "C", "system/UsersPage", "user", PermissionCodes.UsersView, null, false, true, "0", "0", PermissionCodes.UsersView, false),
            ("角色管理", "/settings/auth/roles", "/system", 35, "C", "system/RolesPage", "team", PermissionCodes.RolesView, null, false, true, "0", "0", PermissionCodes.RolesView, false),
            ("个人访问令牌", "/settings/auth/pats", "/system", 36, "C", "settings/PersonalAccessTokensPage", "safety", PermissionCodes.PersonalAccessTokenView, null, false, true, "0", "0", PermissionCodes.PersonalAccessTokenView, false),
            ("菜单管理", "/settings/auth/menus", "/system", 37, "C", "system/MenusPage", "menu", PermissionCodes.MenusView, null, false, true, "0", "0", PermissionCodes.MenusView, false),
            ("项目管理", "/settings/projects", "/system", 38, "C", "system/ProjectsPage", "project", PermissionCodes.ProjectsView, null, false, true, "0", "0", PermissionCodes.ProjectsView, false),
            ("数据源管理", "/settings/system/datasources", "/system", 39, "C", "system/TenantDataSourcesPage", "database", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("字典管理", "/settings/system/dict-types", "/system", 40, "C", "system/DictTypesPage", "book", PermissionCodes.DictTypeView, null, false, true, "0", "0", PermissionCodes.DictTypeView, false),
            ("参数配置", "/settings/system/configs", "/system", 41, "C", "system/SystemConfigsPage", "tool", PermissionCodes.ConfigView, null, false, true, "0", "0", PermissionCodes.ConfigView, false),
            ("AI 管理配置", "/admin/ai-config", "/system", 42, "C", "admin/AiConfigPage", "control", PermissionCodes.AiAdminConfigView, null, false, true, "0", "0", PermissionCodes.AiAdminConfigView, false),
            ("通知中心", "/system/notifications", "/system", 43, "C", "system/NotificationsPage", "notification", PermissionCodes.NotificationView, null, false, true, "0", "0", PermissionCodes.NotificationView, false),
            ("插件管理", "/settings/system/plugins", "/system", 44, "C", "system/PluginManagePage", "api", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("Webhook管理", "/settings/system/webhooks", "/system", 45, "C", "system/WebhooksPage", "link", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("License 授权", "/settings/license", "/system", 46, "C", "LicensePage", "safety-certificate", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("API 连接器", "/settings/system/connectors", "/system", 47, "C", "system/ApiConnectorsPage", "api", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),

            ("用户查询", "/settings/org/users:query", "/settings/org/users", 1, "F", null, null, PermissionCodes.UsersView, null, false, false, "0", "0", PermissionCodes.UsersView, true),
            ("用户新增", "/settings/org/users:create", "/settings/org/users", 2, "F", null, null, PermissionCodes.UsersCreate, null, false, false, "0", "0", PermissionCodes.UsersCreate, true),
            ("用户修改", "/settings/org/users:update", "/settings/org/users", 3, "F", null, null, PermissionCodes.UsersUpdate, null, false, false, "0", "0", PermissionCodes.UsersUpdate, true),
            ("用户删除", "/settings/org/users:delete", "/settings/org/users", 4, "F", null, null, PermissionCodes.UsersDelete, null, false, false, "0", "0", PermissionCodes.UsersDelete, true),
            ("角色查询", "/settings/auth/roles:query", "/settings/auth/roles", 1, "F", null, null, PermissionCodes.RolesView, null, false, false, "0", "0", PermissionCodes.RolesView, true),
            ("角色新增", "/settings/auth/roles:create", "/settings/auth/roles", 2, "F", null, null, PermissionCodes.RolesCreate, null, false, false, "0", "0", PermissionCodes.RolesCreate, true),
            ("角色修改", "/settings/auth/roles:update", "/settings/auth/roles", 3, "F", null, null, PermissionCodes.RolesUpdate, null, false, false, "0", "0", PermissionCodes.RolesUpdate, true),
            ("角色删除", "/settings/auth/roles:delete", "/settings/auth/roles", 4, "F", null, null, PermissionCodes.RolesDelete, null, false, false, "0", "0", PermissionCodes.RolesDelete, true),
            ("菜单查询", "/settings/auth/menus:query", "/settings/auth/menus", 1, "F", null, null, PermissionCodes.MenusView, null, false, false, "0", "0", PermissionCodes.MenusView, true),
            ("菜单新增", "/settings/auth/menus:create", "/settings/auth/menus", 2, "F", null, null, PermissionCodes.MenusCreate, null, false, false, "0", "0", PermissionCodes.MenusCreate, true),
            ("菜单修改", "/settings/auth/menus:update", "/settings/auth/menus", 3, "F", null, null, PermissionCodes.MenusUpdate, null, false, false, "0", "0", PermissionCodes.MenusUpdate, true),
            ("菜单删除", "/settings/auth/menus:delete", "/settings/auth/menus", 4, "F", null, null, PermissionCodes.MenusDelete, null, false, false, "0", "0", PermissionCodes.MenusDelete, true),
            ("PAT 查询", "/settings/auth/pats:query", "/settings/auth/pats", 1, "F", null, null, PermissionCodes.PersonalAccessTokenView, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenView, true),
            ("PAT 新增", "/settings/auth/pats:create", "/settings/auth/pats", 2, "F", null, null, PermissionCodes.PersonalAccessTokenCreate, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenCreate, true),
            ("PAT 修改", "/settings/auth/pats:update", "/settings/auth/pats", 3, "F", null, null, PermissionCodes.PersonalAccessTokenUpdate, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenUpdate, true),
            ("PAT 删除", "/settings/auth/pats:delete", "/settings/auth/pats", 4, "F", null, null, PermissionCodes.PersonalAccessTokenDelete, null, false, false, "0", "0", PermissionCodes.PersonalAccessTokenDelete, true),
            ("AI 配置查询", "/admin/ai-config:query", "/admin/ai-config", 5, "F", null, null, PermissionCodes.AiAdminConfigView, null, false, false, "0", "0", PermissionCodes.AiAdminConfigView, true),
            ("AI 配置更新", "/admin/ai-config:update", "/admin/ai-config", 6, "F", null, null, PermissionCodes.AiAdminConfigUpdate, null, false, false, "0", "0", PermissionCodes.AiAdminConfigUpdate, true),
            ("模型配置查询", "/settings/ai/model-configs:query", "/settings/ai/model-configs", 1, "F", null, null, PermissionCodes.ModelConfigView, null, false, false, "0", "0", PermissionCodes.ModelConfigView, true),
            ("模型配置新增", "/settings/ai/model-configs:create", "/settings/ai/model-configs", 2, "F", null, null, PermissionCodes.ModelConfigCreate, null, false, false, "0", "0", PermissionCodes.ModelConfigCreate, true),
            ("模型配置修改", "/settings/ai/model-configs:update", "/settings/ai/model-configs", 3, "F", null, null, PermissionCodes.ModelConfigUpdate, null, false, false, "0", "0", PermissionCodes.ModelConfigUpdate, true),
            ("模型配置删除", "/settings/ai/model-configs:delete", "/settings/ai/model-configs", 4, "F", null, null, PermissionCodes.ModelConfigDelete, null, false, false, "0", "0", PermissionCodes.ModelConfigDelete, true),
            ("Agent 查询", "/ai/agents:query", "/ai/agents", 1, "F", null, null, PermissionCodes.AgentView, null, false, false, "0", "0", PermissionCodes.AgentView, true),
            ("Agent 新增", "/ai/agents:create", "/ai/agents", 2, "F", null, null, PermissionCodes.AgentCreate, null, false, false, "0", "0", PermissionCodes.AgentCreate, true),
            ("Agent 修改", "/ai/agents:update", "/ai/agents", 3, "F", null, null, PermissionCodes.AgentUpdate, null, false, false, "0", "0", PermissionCodes.AgentUpdate, true),
            ("Agent 删除", "/ai/agents:delete", "/ai/agents", 4, "F", null, null, PermissionCodes.AgentDelete, null, false, false, "0", "0", PermissionCodes.AgentDelete, true),
            ("会话查询", "/ai/conversations:query", "/ai", 5, "F", null, null, PermissionCodes.ConversationView, null, false, false, "0", "0", PermissionCodes.ConversationView, true),
            ("会话新增", "/ai/conversations:create", "/ai", 6, "F", null, null, PermissionCodes.ConversationCreate, null, false, false, "0", "0", PermissionCodes.ConversationCreate, true),
            ("会话删除", "/ai/conversations:delete", "/ai", 7, "F", null, null, PermissionCodes.ConversationDelete, null, false, false, "0", "0", PermissionCodes.ConversationDelete, true),
            ("知识库查询", "/ai/knowledge-bases:query", "/ai/knowledge-bases", 8, "F", null, null, PermissionCodes.KnowledgeBaseView, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseView, true),
            ("知识库新增", "/ai/knowledge-bases:create", "/ai/knowledge-bases", 9, "F", null, null, PermissionCodes.KnowledgeBaseCreate, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseCreate, true),
            ("知识库修改", "/ai/knowledge-bases:update", "/ai/knowledge-bases", 10, "F", null, null, PermissionCodes.KnowledgeBaseUpdate, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseUpdate, true),
            ("知识库删除", "/ai/knowledge-bases:delete", "/ai/knowledge-bases", 11, "F", null, null, PermissionCodes.KnowledgeBaseDelete, null, false, false, "0", "0", PermissionCodes.KnowledgeBaseDelete, true),
            ("数据库查询", "/ai/databases:query", "/ai/databases", 12, "F", null, null, PermissionCodes.AiDatabaseView, null, false, false, "0", "0", PermissionCodes.AiDatabaseView, true),
            ("数据库新增", "/ai/databases:create", "/ai/databases", 13, "F", null, null, PermissionCodes.AiDatabaseCreate, null, false, false, "0", "0", PermissionCodes.AiDatabaseCreate, true),
            ("数据库修改", "/ai/databases:update", "/ai/databases", 14, "F", null, null, PermissionCodes.AiDatabaseUpdate, null, false, false, "0", "0", PermissionCodes.AiDatabaseUpdate, true),
            ("数据库删除", "/ai/databases:delete", "/ai/databases", 15, "F", null, null, PermissionCodes.AiDatabaseDelete, null, false, false, "0", "0", PermissionCodes.AiDatabaseDelete, true),
            ("变量查询", "/ai/variables:query", "/ai/variables", 16, "F", null, null, PermissionCodes.AiVariableView, null, false, false, "0", "0", PermissionCodes.AiVariableView, true),
            ("变量新增", "/ai/variables:create", "/ai/variables", 17, "F", null, null, PermissionCodes.AiVariableCreate, null, false, false, "0", "0", PermissionCodes.AiVariableCreate, true),
            ("变量修改", "/ai/variables:update", "/ai/variables", 18, "F", null, null, PermissionCodes.AiVariableUpdate, null, false, false, "0", "0", PermissionCodes.AiVariableUpdate, true),
            ("变量删除", "/ai/variables:delete", "/ai/variables", 19, "F", null, null, PermissionCodes.AiVariableDelete, null, false, false, "0", "0", PermissionCodes.AiVariableDelete, true),
            ("插件查询", "/ai/plugins:query", "/ai/plugins", 20, "F", null, null, PermissionCodes.AiPluginView, null, false, false, "0", "0", PermissionCodes.AiPluginView, true),
            ("插件新增", "/ai/plugins:create", "/ai/plugins", 21, "F", null, null, PermissionCodes.AiPluginCreate, null, false, false, "0", "0", PermissionCodes.AiPluginCreate, true),
            ("插件修改", "/ai/plugins:update", "/ai/plugins", 22, "F", null, null, PermissionCodes.AiPluginUpdate, null, false, false, "0", "0", PermissionCodes.AiPluginUpdate, true),
            ("插件删除", "/ai/plugins:delete", "/ai/plugins", 23, "F", null, null, PermissionCodes.AiPluginDelete, null, false, false, "0", "0", PermissionCodes.AiPluginDelete, true),
            ("插件发布", "/ai/plugins:publish", "/ai/plugins", 24, "F", null, null, PermissionCodes.AiPluginPublish, null, false, false, "0", "0", PermissionCodes.AiPluginPublish, true),
            ("插件调试", "/ai/plugins:debug", "/ai/plugins", 25, "F", null, null, PermissionCodes.AiPluginDebug, null, false, false, "0", "0", PermissionCodes.AiPluginDebug, true),
            ("应用查询", "/ai/apps:query", "/ai/apps", 26, "F", null, null, PermissionCodes.AiAppView, null, false, false, "0", "0", PermissionCodes.AiAppView, true),
            ("应用新增", "/ai/apps:create", "/ai/apps", 27, "F", null, null, PermissionCodes.AiAppCreate, null, false, false, "0", "0", PermissionCodes.AiAppCreate, true),
            ("应用修改", "/ai/apps:update", "/ai/apps", 28, "F", null, null, PermissionCodes.AiAppUpdate, null, false, false, "0", "0", PermissionCodes.AiAppUpdate, true),
            ("应用删除", "/ai/apps:delete", "/ai/apps", 29, "F", null, null, PermissionCodes.AiAppDelete, null, false, false, "0", "0", PermissionCodes.AiAppDelete, true),
            ("应用发布", "/ai/apps:publish", "/ai/apps", 30, "F", null, null, PermissionCodes.AiAppPublish, null, false, false, "0", "0", PermissionCodes.AiAppPublish, true),
            ("Prompt 查询", "/ai/prompts:query", "/ai/prompts", 31, "F", null, null, PermissionCodes.AiPromptView, null, false, false, "0", "0", PermissionCodes.AiPromptView, true),
            ("Prompt 新增", "/ai/prompts:create", "/ai/prompts", 32, "F", null, null, PermissionCodes.AiPromptCreate, null, false, false, "0", "0", PermissionCodes.AiPromptCreate, true),
            ("Prompt 修改", "/ai/prompts:update", "/ai/prompts", 33, "F", null, null, PermissionCodes.AiPromptUpdate, null, false, false, "0", "0", PermissionCodes.AiPromptUpdate, true),
            ("Prompt 删除", "/ai/prompts:delete", "/ai/prompts", 34, "F", null, null, PermissionCodes.AiPromptDelete, null, false, false, "0", "0", PermissionCodes.AiPromptDelete, true),
            ("市场查询", "/ai/marketplace:query", "/ai/marketplace", 35, "F", null, null, PermissionCodes.AiMarketplaceView, null, false, false, "0", "0", PermissionCodes.AiMarketplaceView, true),
            ("市场新增", "/ai/marketplace:create", "/ai/marketplace", 36, "F", null, null, PermissionCodes.AiMarketplaceCreate, null, false, false, "0", "0", PermissionCodes.AiMarketplaceCreate, true),
            ("市场修改", "/ai/marketplace:update", "/ai/marketplace", 37, "F", null, null, PermissionCodes.AiMarketplaceUpdate, null, false, false, "0", "0", PermissionCodes.AiMarketplaceUpdate, true),
            ("市场删除", "/ai/marketplace:delete", "/ai/marketplace", 38, "F", null, null, PermissionCodes.AiMarketplaceDelete, null, false, false, "0", "0", PermissionCodes.AiMarketplaceDelete, true),
            ("市场发布", "/ai/marketplace:publish", "/ai/marketplace", 39, "F", null, null, PermissionCodes.AiMarketplacePublish, null, false, false, "0", "0", PermissionCodes.AiMarketplacePublish, true),
            ("搜索查询", "/ai/search:query", "/ai/search", 40, "F", null, null, PermissionCodes.AiSearchView, null, false, false, "0", "0", PermissionCodes.AiSearchView, true),
            ("搜索记录更新", "/ai/search:update", "/ai/search", 41, "F", null, null, PermissionCodes.AiSearchUpdate, null, false, false, "0", "0", PermissionCodes.AiSearchUpdate, true),
            ("工作台查询", "/ai/workspace:query", "/ai/workspace", 42, "F", null, null, PermissionCodes.AiWorkspaceView, null, false, false, "0", "0", PermissionCodes.AiWorkspaceView, true),
            ("工作台更新", "/ai/workspace:update", "/ai/workspace", 43, "F", null, null, PermissionCodes.AiWorkspaceUpdate, null, false, false, "0", "0", PermissionCodes.AiWorkspaceUpdate, true),
            ("DevOps 查看", "/ai/devops:query", "/ai/devops/test-sets", 44, "F", null, null, PermissionCodes.AiDevopsView, null, false, false, "0", "0", PermissionCodes.AiDevopsView, true),
            ("快捷命令查看", "/ai/shortcuts:query", "/ai/shortcuts", 45, "F", null, null, PermissionCodes.AiShortcutView, null, false, false, "0", "0", PermissionCodes.AiShortcutView, true),
            ("快捷命令管理", "/ai/shortcuts:manage", "/ai/shortcuts", 46, "F", null, null, PermissionCodes.AiShortcutManage, null, false, false, "0", "0", PermissionCodes.AiShortcutManage, true)
        };

        var menuPaths = menuSeeds.Select(x => x.Path).Distinct().ToArray();
        var existingMenus = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(menuPaths, x.Path))
            .ToListAsync(cancellationToken);
        var menuIdMap = existingMenus.ToDictionary(x => x.Path, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var menusToInsert = new List<Menu>();
        foreach (var seed in menuSeeds)
        {
            if (menuIdMap.ContainsKey(seed.Path))
            {
                continue;
            }

            var parentId = 0L;
            if (!string.IsNullOrWhiteSpace(seed.ParentPath) && menuIdMap.TryGetValue(seed.ParentPath, out var resolvedParentId))
            {
                parentId = resolvedParentId;
            }

            var menu = new Menu(
                tenantId,
                seed.Name,
                seed.Path,
                idGeneratorAccessor.NextId(),
                parentId,
                seed.SortOrder,
                seed.MenuType,
                seed.Component,
                seed.Icon,
                seed.Perms,
                seed.Query,
                seed.IsFrame,
                seed.IsCache,
                seed.Visible,
                seed.Status,
                seed.PermissionCode,
                seed.IsHidden);
            menusToInsert.Add(menu);
            menuIdMap[seed.Path] = menu.Id;
        }

        if (menusToInsert.Count > 0)
        {
            await db.Insertable(menusToInsert).ExecuteCommandAsync(cancellationToken);
        }

        var departmentSeeds = ResolveDepartmentSeeds();
        if (departmentSeeds.Count > 0)
        {
            var departmentNameList = departmentSeeds.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var existingDepartments = await db.Queryable<Department>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(departmentNameList, x.Name))
                .ToListAsync(cancellationToken);
            var departmentIdMap = existingDepartments.ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);

            var departmentsToInsert = new List<Department>();
            foreach (var seed in departmentSeeds)
            {
                if (departmentIdMap.ContainsKey(seed.Code))
                {
                    continue;
                }

                var parentId = 0L;
                if (!string.IsNullOrWhiteSpace(seed.ParentCode) &&
                    departmentIdMap.TryGetValue(seed.ParentCode, out var resolvedParentId))
                {
                    parentId = resolvedParentId;
                }

                var department = new Department(tenantId, seed.Name, seed.Code, idGeneratorAccessor.NextId(), parentId, seed.SortOrder);
                departmentsToInsert.Add(department);
                departmentIdMap[seed.Code] = department.Id;
            }

            if (departmentsToInsert.Count > 0)
            {
                await db.Insertable(departmentsToInsert).ExecuteCommandAsync(cancellationToken);
            }

            report.DepartmentsCreated = departmentsToInsert.Count;
        }

        var positionSeeds = ResolvePositionSeeds();
        if (positionSeeds.Count > 0)
        {
            var positionCodeList = positionSeeds.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var existingPositions = await db.Queryable<Position>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(positionCodeList, x.Code))
                .ToListAsync(cancellationToken);
            var positionCodeMap = existingPositions.ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);

            var positionsToInsert = new List<Position>();
            foreach (var seed in positionSeeds)
            {
                if (positionCodeMap.ContainsKey(seed.Code))
                {
                    continue;
                }

                var position = new Position(tenantId, seed.Name, seed.Code, idGeneratorAccessor.NextId());
                position.Update(seed.Name, seed.Description, true, seed.SortOrder);
                if (seed.IsSystem)
                {
                    position.MarkSystem();
                }

                positionsToInsert.Add(position);
                positionCodeMap[seed.Code] = position.Id;
            }

            if (positionsToInsert.Count > 0)
            {
                await db.Insertable(positionsToInsert).ExecuteCommandAsync(cancellationToken);
            }

            report.PositionsCreated = positionsToInsert.Count;
        }

        var existingRolePermissions = await rolePermissionRepository.QueryByRoleIdsAsync(
            tenantId,
            roleIds,
            cancellationToken);
        var existingRolePermissionSet = existingRolePermissions
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        var rolePermissionsToInsert = new List<RolePermission>();
        foreach (var roleCode in roleCodesArray)
        {
            if (!roleMap.TryGetValue(roleCode, out var roleForPermissions))
            {
                continue;
            }

            foreach (var permissionCode in EnumerateBootstrapRoleSeedPermissionCodes(roleCode, permissionIdMap))
            {
                if (!permissionIdMap.TryGetValue(permissionCode, out var permissionId))
                {
                    continue;
                }

                if (existingRolePermissionSet.Contains((roleForPermissions.Id, permissionId)))
                {
                    continue;
                }

                rolePermissionsToInsert.Add(
                    new RolePermission(tenantId, roleForPermissions.Id, permissionId, idGeneratorAccessor.NextId()));
            }
        }

        if (rolePermissionsToInsert.Count > 0)
        {
            await rolePermissionRepository.AddRangeAsync(rolePermissionsToInsert, cancellationToken);
        }

        var requiredMenuPaths = new[]
        {
            "/",
            "/security",
            "/assets",
            "/audit",
            "/alert",
            "/ai",
            "/settings/ai/model-configs",
            "/ai/agents",
            "/ai/knowledge-bases",
            "/ai/memories",
            "/ai/databases",
            "/ai/variables",
            "/ai/plugins",
            "/ai/apps",
            "/ai/prompts",
            "/ai/open-platform",
            "/ai/marketplace",
            "/ai/search",
            "/ai/workspace",
            "/ai/library",
            "/ai/devops/test-sets",
            "/ai/devops/mock-sets",
            "/ai/shortcuts",
            "/process",
            "/approval/flows",
            "/process/start",
            "/process/inbox",
            "/process/done",
            "/process/my-requests",
            "/process/cc",
            "/process/designer",
            "/process/designer/:id",
            "/process/tasks/:id",
            "/process/instances/:id",
            "/process/manage/flows",
            "/process/manage/instances",
            "/workflow/designer",
            "/monitor",
            "/monitor/message-queue",
            "/system",
            "/settings/org/departments",
            "/settings/org/tenants",
            "/settings/org/positions",
            "/settings/org/users",
            "/settings/auth/roles",
            "/settings/auth/pats",
            "/settings/auth/menus",
            "/settings/projects",
            "/settings/system/datasources",
            "/settings/system/dict-types",
            "/settings/system/configs",
            "/admin/ai-config",
            "/system/notifications",
            "/settings/system/plugins",
            "/settings/system/webhooks"
        };
        var menuIds = requiredMenuPaths
            .Select(path => menuIdMap.TryGetValue(path, out var menuId) ? menuId : 0)
            .Where(menuId => menuId > 0)
            .Distinct()
            .ToArray();

        var roleIdArray = roleIds.Distinct().ToArray();
        var existingRoleMenus = await db.Queryable<RoleMenu>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && SqlFunc.ContainsArray(roleIdArray, x.RoleId)
                && SqlFunc.ContainsArray(menuIds, x.MenuId))
            .Select(x => new { x.RoleId, x.MenuId })
            .ToListAsync(cancellationToken);
        var existingRoleMenuSet = existingRoleMenus
            .Select(x => (x.RoleId, x.MenuId))
            .ToHashSet();

        var roleMenusToInsert = new List<RoleMenu>();
        foreach (var roleId in roleIds)
        {
            foreach (var menuId in menuIds)
            {
                if (existingRoleMenuSet.Contains((roleId, menuId)))
                {
                    continue;
                }

                roleMenusToInsert.Add(new RoleMenu(tenantId, roleId, menuId, idGeneratorAccessor.NextId()));
            }
        }

        if (roleMenusToInsert.Count > 0)
        {
            await roleMenuRepository.AddRangeAsync(roleMenusToInsert, cancellationToken);
        }

        var existing = await userRepository.FindByUsernameAsync(tenantId, effectiveBootstrap.Username, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateRoles(string.Join(',', roleCodes));
            if (effectiveBootstrap.IsPlatformAdmin)
            {
                existing.MarkPlatformAdmin();
            }
            else
            {
                existing.UnmarkPlatformAdmin();
            }

            await userRepository.UpdateAsync(existing, cancellationToken);
            await userRoleRepository.DeleteByUserIdAsync(tenantId, existing.Id, cancellationToken);
            await userRoleRepository.AddRangeAsync(
                roleIds.Select(roleId => new UserRole(tenantId, existing.Id, roleId, idGeneratorAccessor.NextId())).ToArray(),
                cancellationToken);
            report.AdminCreated = true;
            report.AdminUsername = effectiveBootstrap.Username;
            var existingAdminPermissionCheckResult = await EnsureBootstrapAdminMaxPermissionsAsync(
                scope,
                appContextAccessor,
                db,
                effectiveBootstrap,
                cancellationToken);
            ApplyAdminPermissionCheckReport(report, existingAdminPermissionCheckResult);
            await EnsureDefaultWorkspacesAsync(scope.ServiceProvider, db, cancellationToken);
            return report;
        }

        var hashed = passwordHasher.HashPassword(effectiveBootstrap.Password);
        var account = new UserAccount(tenantId, effectiveBootstrap.Username, effectiveBootstrap.Username, hashed, idGeneratorAccessor.NextId());
        account.UpdateRoles(string.Join(',', roleCodes));
        account.MarkSystemAccount();
        if (effectiveBootstrap.IsPlatformAdmin)
        {
            account.MarkPlatformAdmin();
        }
        await userRepository.AddAsync(account, cancellationToken);
        await userRoleRepository.AddRangeAsync(
            roleIds.Select(roleId => new UserRole(tenantId, account.Id, roleId, idGeneratorAccessor.NextId())).ToArray(),
            cancellationToken);
        report.AdminCreated = true;
        report.AdminUsername = effectiveBootstrap.Username;
        _logger.LogInformation("已创建BootstrapAdmin账号：{Username}", effectiveBootstrap.Username);

        var adminPermissionCheckResult = await EnsureBootstrapAdminMaxPermissionsAsync(
            scope,
            appContextAccessor,
            db,
            effectiveBootstrap,
            cancellationToken);
        ApplyAdminPermissionCheckReport(report, adminPermissionCheckResult);

        await EnsureDefaultWorkspacesAsync(scope.ServiceProvider, db, cancellationToken);

        return report;
        }
        finally
        {
            if (setupOwnedDb is IDisposable disposableDb)
            {
                disposableDb.Dispose();
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureTenantAppDataSourceBindingBackfillAsync(
        IServiceProvider serviceProvider,
        IAppContextAccessor appContextAccessor,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("TenantAppDataSourceBinding", false)
            || !db.DbMaintenance.IsAnyTable("AppManifest", false))
        {
            return;
        }

        var legacyBindings = await db.Queryable<AppManifest>()
            .Where(item => item.DataSourceId.HasValue)
            .Select(item => new LegacyAppBindingProjection
            {
                TenantIdValue = item.TenantIdValue,
                TenantAppInstanceId = item.Id,
                DataSourceId = item.DataSourceId!.Value,
                CreatedBy = item.CreatedBy,
                UpdatedBy = item.UpdatedBy,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        if (legacyBindings.Count == 0)
        {
            return;
        }

        var appIds = legacyBindings.Select(item => item.TenantAppInstanceId).Distinct().ToArray();
        var tenantIds = legacyBindings.Select(item => item.TenantIdValue).Distinct().ToArray();
        var existedAppKeys = await db.Queryable<TenantAppDataSourceBinding>()
            .Where(item => SqlFunc.ContainsArray(appIds, item.TenantAppInstanceId)
                && SqlFunc.ContainsArray(tenantIds, item.TenantIdValue))
            .Select(item => new { item.TenantIdValue, item.TenantAppInstanceId })
            .ToListAsync(cancellationToken);
        var existedAppSet = existedAppKeys
            .Select(item => (item.TenantIdValue, item.TenantAppInstanceId))
            .ToHashSet();

        var idGenerator = serviceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var backfillRows = new List<TenantAppDataSourceBinding>();
        foreach (var legacy in legacyBindings)
        {
            if (existedAppSet.Contains((legacy.TenantIdValue, legacy.TenantAppInstanceId)))
            {
                continue;
            }

            var now = legacy.UpdatedAt;
            var actor = legacy.UpdatedBy > 0 ? legacy.UpdatedBy : legacy.CreatedBy;
            using var appContextScope = appContextAccessor.BeginScope(
                CreateSystemContext(appContextAccessor, new TenantId(legacy.TenantIdValue)));
            var binding = new TenantAppDataSourceBinding(
                new TenantId(legacy.TenantIdValue),
                legacy.TenantAppInstanceId,
                legacy.DataSourceId,
                TenantAppDataSourceBindingType.Primary,
                actor,
                idGenerator.NextId(),
                now,
                "Migrated from AppManifest.DataSourceId");
            backfillRows.Add(binding);
        }

        if (backfillRows.Count == 0)
        {
            return;
        }

        await db.Insertable(backfillRows).ExecuteCommandAsync(cancellationToken);
        _logger.LogInformation(
            "[DatabaseInitializer] 已回填 TenantAppDataSourceBinding 记录 {Count} 条。",
            backfillRows.Count);
    }

    private async Task EnsureTenantAppDataSourceBindingHealthAsync(
        IServiceProvider serviceProvider,
        IAppContextAccessor appContextAccessor,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("TenantAppDataSourceBinding", false)
            || !db.DbMaintenance.IsAnyTable("AppManifest", false)
            || !db.DbMaintenance.IsAnyTable("TenantDataSource", false))
        {
            return;
        }

        var apps = await db.Queryable<AppManifest>()
            .Select(item => new AppBindingHealthProjection
            {
                TenantIdValue = item.TenantIdValue,
                AppInstanceId = item.Id,
                DataSourceId = item.DataSourceId,
                UpdatedBy = item.UpdatedBy,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        if (apps.Count == 0)
        {
            return;
        }

        var tenantIds = apps.Select(item => item.TenantIdValue).Distinct().ToArray();
        var appIds = apps.Select(item => item.AppInstanceId).Distinct().ToArray();
        var dataSources = await db.Queryable<TenantDataSource>()
            .Where(item => SqlFunc.ContainsArray(tenantIds, item.TenantIdValue))
            .Select(item => new DataSourceHealthProjection
            {
                TenantIdValue = item.TenantIdValue,
                DataSourceId = item.Id,
                IsActive = item.IsActive
            })
            .ToListAsync(cancellationToken);
        var bindings = await db.Queryable<TenantAppDataSourceBinding>()
            .Where(item =>
                SqlFunc.ContainsArray(tenantIds, item.TenantIdValue)
                && SqlFunc.ContainsArray(appIds, item.TenantAppInstanceId))
            .ToListAsync(cancellationToken);

        var appKeySet = apps
            .Select(item => (item.TenantIdValue, item.AppInstanceId))
            .ToHashSet();
        var activeDataSourceSet = dataSources
            .Where(item => item.IsActive)
            .Select(item => (item.TenantIdValue, item.DataSourceId))
            .ToHashSet();
        var now = DateTimeOffset.UtcNow;

        var invalidBindings = bindings
            .Where(item =>
                item.IsActive
                && (!appKeySet.Contains((item.TenantIdValue, item.TenantAppInstanceId))
                    || !activeDataSourceSet.Contains((item.TenantIdValue.ToString(), item.DataSourceId))))
            .ToList();
        foreach (var binding in invalidBindings)
        {
            binding.Deactivate(0, now, "DatabaseInitializer:deactivate-invalid-binding");
        }

        var activeBindings = bindings
            .Where(item =>
                item.IsActive
                && appKeySet.Contains((item.TenantIdValue, item.TenantAppInstanceId))
                && activeDataSourceSet.Contains((item.TenantIdValue.ToString(), item.DataSourceId)))
            .ToList();
        var promotedBindings = activeBindings
            .GroupBy(item => (item.TenantIdValue, item.TenantAppInstanceId))
            .Where(group => group.All(item => item.BindingType != TenantAppDataSourceBindingType.Primary))
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAt ?? item.BoundAt)
                .ThenByDescending(item => item.Id)
                .First())
            .ToList();
        foreach (var binding in promotedBindings)
        {
            binding.Rebind(
                binding.DataSourceId,
                TenantAppDataSourceBindingType.Primary,
                0,
                now,
                "DatabaseInitializer:promote-primary-binding");
        }

        var existingActiveBindingKeys = activeBindings
            .Select(item => (item.TenantIdValue, item.TenantAppInstanceId))
            .ToHashSet();
        var idGenerator = serviceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var recoveryBindings = new List<TenantAppDataSourceBinding>();
        foreach (var app in apps.Where(item =>
                     item.DataSourceId.HasValue
                     && activeDataSourceSet.Contains((item.TenantIdValue.ToString(), item.DataSourceId.Value))
                     && !existingActiveBindingKeys.Contains((item.TenantIdValue, item.AppInstanceId))))
        {
            using var appContextScope = appContextAccessor.BeginScope(
                CreateSystemContext(appContextAccessor, new TenantId(app.TenantIdValue)));
            recoveryBindings.Add(new TenantAppDataSourceBinding(
                new TenantId(app.TenantIdValue),
                app.AppInstanceId,
                app.DataSourceId!.Value,
                TenantAppDataSourceBindingType.Primary,
                app.UpdatedBy > 0 ? app.UpdatedBy : 0,
                idGenerator.NextId(),
                app.UpdatedAt,
                "DatabaseInitializer:recover-primary-binding"));
        }

        if (invalidBindings.Count > 0 || promotedBindings.Count > 0)
        {
            await db.Updateable(invalidBindings.Concat(promotedBindings).Distinct().ToArray())
                .ExecuteCommandAsync(cancellationToken);
        }

        if (recoveryBindings.Count > 0)
        {
            await db.Insertable(recoveryBindings).ExecuteCommandAsync(cancellationToken);
        }

        if (invalidBindings.Count > 0 || promotedBindings.Count > 0 || recoveryBindings.Count > 0)
        {
            _logger.LogInformation(
                "[DatabaseInitializer] TenantAppDataSourceBinding 健康修复完成。Deactivated={Deactivated}, Promoted={Promoted}, Recovered={Recovered}",
                invalidBindings.Count,
                promotedBindings.Count,
                recoveryBindings.Count);
        }
    }

    private async Task EnsureOrphanAppProvisioningAsync(
        IServiceProvider serviceProvider,
        IAppContextAccessor appContextAccessor,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AppManifest", false)
            || !db.DbMaintenance.IsAnyTable("TenantAppDataSourceBinding", false)
            || !db.DbMaintenance.IsAnyTable("AppDataRoutePolicy", false))
        {
            return;
        }

        var apps = await db.Queryable<AppManifest>()
            .Select(x => new
            {
                x.TenantIdValue,
                x.Id,
                x.AppKey,
                x.UpdatedBy
            })
            .ToListAsync(cancellationToken);
        if (apps.Count == 0)
        {
            return;
        }

        var tenantIds = apps.Select(x => x.TenantIdValue).Distinct().ToArray();
        var appIds = apps.Select(x => x.Id).Distinct().ToArray();
        var hasBindings = await db.Queryable<TenantAppDataSourceBinding>()
            .Where(x =>
                SqlFunc.ContainsArray(tenantIds, x.TenantIdValue)
                && SqlFunc.ContainsArray(appIds, x.TenantAppInstanceId)
                && x.IsActive)
            .Select(x => new { x.TenantIdValue, x.TenantAppInstanceId })
            .ToListAsync(cancellationToken);
        var hasPolicies = await db.Queryable<AppDataRoutePolicy>()
            .Where(x =>
                SqlFunc.ContainsArray(tenantIds, x.TenantIdValue)
                && SqlFunc.ContainsArray(appIds, x.AppInstanceId))
            .Select(x => new { x.TenantIdValue, AppInstanceId = x.AppInstanceId })
            .ToListAsync(cancellationToken);
        var bindingSet = hasBindings.Select(x => (x.TenantIdValue, x.TenantAppInstanceId)).ToHashSet();
        var policySet = hasPolicies.Select(x => (x.TenantIdValue, x.AppInstanceId)).ToHashSet();
        var orphans = apps.Where(x => !bindingSet.Contains((x.TenantIdValue, x.Id)) && !policySet.Contains((x.TenantIdValue, x.Id)))
            .ToArray();
        if (orphans.Length == 0)
        {
            return;
        }

        var provisioner = serviceProvider.GetRequiredService<IAppDataSourceProvisioner>();
        var repairedCount = 0;
        foreach (var orphan in orphans)
        {
            using var appContextScope = appContextAccessor.BeginScope(
                CreateSystemContext(appContextAccessor, new TenantId(orphan.TenantIdValue)));
            await provisioner.EnsureProvisionedAsync(
                new TenantId(orphan.TenantIdValue),
                orphan.Id,
                orphan.AppKey,
                orphan.UpdatedBy > 0 ? orphan.UpdatedBy : 0,
                preferredDataSourceId: null,
                cancellationToken);
            repairedCount++;
        }

        _logger.LogInformation(
            "[DatabaseInitializer] 孤儿应用数据源供给修复完成。Repaired={Repaired}",
            repairedCount);
    }

    private async Task LoadLicenseStatusAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var licenseService = scope.ServiceProvider.GetService<Atlas.Application.License.Abstractions.ILicenseService>();
            if (licenseService is not null)
            {
                await licenseService.ReloadAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "启动时加载授权证书状态失败，平台将以无授权模式运行");
        }
    }

    private async Task<BootstrapAdminPermissionCheckResult> EnsureBootstrapAdminMaxPermissionsAsync(
        IServiceScope scope,
        IAppContextAccessor appContextAccessor,
        ISqlSugarClient db,
        BootstrapAdminOptions bootstrapOptions,
        CancellationToken cancellationToken)
    {
        if (!bootstrapOptions.Enabled)
        {
            return BootstrapAdminPermissionCheckResult.Success([], "BootstrapAdmin 已禁用，跳过超管权限一致性校验。");
        }

        if (!Guid.TryParse(bootstrapOptions.TenantId, out var tenantGuid))
        {
            return BootstrapAdminPermissionCheckResult.Fail("BootstrapAdmin TenantId 配置无效，无法执行超管权限一致性校验。");
        }

        var tenantId = new TenantId(tenantGuid);
        using var appContextScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, tenantId));
        var idGeneratorAccessor = scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var userRoleRepository = scope.ServiceProvider.GetRequiredService<IUserRoleRepository>();
        var rolePermissionRepository = scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>();
        var roleMenuRepository = scope.ServiceProvider.GetRequiredService<IRoleMenuRepository>();

        var account = await userRepository.FindByUsernameAsync(tenantId, bootstrapOptions.Username, cancellationToken);
        if (account is null)
        {
            return BootstrapAdminPermissionCheckResult.Fail($"未找到 BootstrapAdmin 用户：{bootstrapOptions.Username}。");
        }

        var superAdminRole = (await roleRepository.QueryByCodesAsync(tenantId, ["SuperAdmin"], cancellationToken))
            .FirstOrDefault();

        if (superAdminRole is null)
        {
            superAdminRole = new Role(tenantId, "超级管理员", "SuperAdmin", idGeneratorAccessor.NextId());
            superAdminRole.Update("超级管理员", "平台超级管理员（全量权限）");
            superAdminRole.SetDataScope(DataScopeType.All);
            superAdminRole.MarkSystemRole();
            await roleRepository.AddRangeAsync([superAdminRole], cancellationToken);
        }
        else
        {
            var roleUpdated = false;
            if (superAdminRole.DataScope != DataScopeType.All)
            {
                superAdminRole.SetDataScope(DataScopeType.All);
                roleUpdated = true;
            }

            if (!superAdminRole.IsSystem)
            {
                superAdminRole.MarkSystemRole();
                roleUpdated = true;
            }

            if (roleUpdated)
            {
                await roleRepository.UpdateAsync(superAdminRole, cancellationToken);
            }
        }

        if (!account.IsPlatformAdmin)
        {
            account.MarkPlatformAdmin();
            await userRepository.UpdateAsync(account, cancellationToken);
        }

        var userRoleIds = (await userRoleRepository.QueryByUserIdAsync(tenantId, account.Id, cancellationToken))
            .Select(item => item.RoleId)
            .ToHashSet();
        if (!userRoleIds.Contains(superAdminRole.Id))
        {
            await userRoleRepository.AddRangeAsync(
                [new UserRole(tenantId, account.Id, superAdminRole.Id, idGeneratorAccessor.NextId())],
                cancellationToken);
        }

        var permissionIds = await db.Queryable<Permission>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (permissionIds.Count > 0)
        {
            var existingRolePermissionIds = (await rolePermissionRepository.QueryByRoleIdsAsync(
                    tenantId,
                    [superAdminRole.Id],
                    cancellationToken))
                .Where(item => item.RoleId == superAdminRole.Id)
                .Select(item => item.PermissionId)
                .ToHashSet();
            var missingRolePermissions = permissionIds
                .Where(permissionId => !existingRolePermissionIds.Contains(permissionId))
                .Select(permissionId => new RolePermission(tenantId, superAdminRole.Id, permissionId, idGeneratorAccessor.NextId()))
                .ToArray();
            if (missingRolePermissions.Length > 0)
            {
                await rolePermissionRepository.AddRangeAsync(missingRolePermissions, cancellationToken);
            }
        }

        var menuIds = await db.Queryable<Menu>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (menuIds.Count > 0)
        {
            var existingRoleMenuIds = await db.Queryable<RoleMenu>()
                .Where(item => item.TenantIdValue == tenantId.Value
                    && item.RoleId == superAdminRole.Id
                    && SqlFunc.ContainsArray(menuIds.ToArray(), item.MenuId))
                .Select(item => item.MenuId)
                .ToListAsync(cancellationToken);
            var existingRoleMenuSet = existingRoleMenuIds.ToHashSet();
            var missingRoleMenus = menuIds
                .Where(menuId => !existingRoleMenuSet.Contains(menuId))
                .Select(menuId => new RoleMenu(tenantId, superAdminRole.Id, menuId, idGeneratorAccessor.NextId()))
                .ToArray();
            if (missingRoleMenus.Length > 0)
            {
                await roleMenuRepository.AddRangeAsync(missingRoleMenus, cancellationToken);
            }
        }

        var effectiveAdminRoles = await db.Queryable<UserRole, Role>((ur, role) => ur.RoleId == role.Id)
            .Where((ur, role) => ur.TenantIdValue == tenantId.Value
                && role.TenantIdValue == tenantId.Value
                && ur.UserId == account.Id)
            .Select((ur, role) => role.Code)
            .ToListAsync(cancellationToken);
        effectiveAdminRoles = effectiveAdminRoles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasSuperAdmin = effectiveAdminRoles.Any(code => string.Equals(code, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
        var checkPassed = hasSuperAdmin && account.IsPlatformAdmin;
        var checkMessage = checkPassed
            ? "BootstrapAdmin 已拥有 SuperAdmin、平台管理员标记及全量权限菜单绑定。"
            : "BootstrapAdmin 最大权限校验未通过，请检查用户角色与平台管理员标记。";

        return checkPassed
            ? BootstrapAdminPermissionCheckResult.Success(effectiveAdminRoles, checkMessage)
            : BootstrapAdminPermissionCheckResult.Fail(checkMessage, effectiveAdminRoles);
    }

    private static void ApplyAdminPermissionCheckReport(BootstrapReport report, BootstrapAdminPermissionCheckResult result)
    {
        report.EffectiveAdminRoles = result.EffectiveAdminRoles;
        report.AdminPermissionCheckPassed = result.Passed;
        report.AdminPermissionCheckMessage = result.Message;
    }

    private sealed record BootstrapAdminPermissionCheckResult(
        bool Passed,
        List<string> EffectiveAdminRoles,
        string Message)
    {
        public static BootstrapAdminPermissionCheckResult Success(
            IReadOnlyCollection<string> effectiveAdminRoles,
            string message)
        {
            return new BootstrapAdminPermissionCheckResult(true, effectiveAdminRoles.ToList(), message);
        }

        public static BootstrapAdminPermissionCheckResult Fail(
            string message,
            IReadOnlyCollection<string>? effectiveAdminRoles = null)
        {
            return new BootstrapAdminPermissionCheckResult(
                false,
                effectiveAdminRoles?.ToList() ?? [],
                message);
        }
    }

    private static IAppContext CreateSystemContext(IAppContextAccessor appContextAccessor, TenantId tenantId)
    {
        var appId = appContextAccessor.GetAppId();
        var clientContext = new ClientContext(
            ClientType.Backend,
            ClientPlatform.Web,
            ClientChannel.App,
            ClientAgent.Other);
        return new AppContextSnapshot(tenantId, appId, null, clientContext, null);
    }

    private static async Task EnsureUserAccountSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("UserAccount", false))
        {
            return;
        }

        if (!RequiresMissingColumnFix<UserAccount>(db, "IsPlatformAdmin"))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await db.Ado.ExecuteCommandAsync(
            "ALTER TABLE UserAccount ADD COLUMN IsPlatformAdmin INTEGER NOT NULL DEFAULT 0");
    }

    private static async Task<SchemaAlignmentReport?> EnsureAppMembershipSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var missingTables =
            !db.DbMaintenance.IsAnyTable("AppMember", false)
            || !db.DbMaintenance.IsAnyTable("AppRole", false)
            || !db.DbMaintenance.IsAnyTable("AppUserRole", false)
            || !db.DbMaintenance.IsAnyTable("AppRolePermission", false)
            || !db.DbMaintenance.IsAnyTable("AppPermission", false)
            || !db.DbMaintenance.IsAnyTable("AppRolePage", false)
            || !db.DbMaintenance.IsAnyTable("AppDepartment", false)
            || !db.DbMaintenance.IsAnyTable("AppPosition", false)
            || !db.DbMaintenance.IsAnyTable("AppProject", false)
            || !db.DbMaintenance.IsAnyTable("AppProjectUser", false)
            || !db.DbMaintenance.IsAnyTable("AppMemberDepartment", false)
            || !db.DbMaintenance.IsAnyTable("AppMemberPosition", false);
        if (missingTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<AppMember, AppMemberDepartment, AppMemberPosition>();
            db.CodeFirst.InitTables<AppRole, AppUserRole, AppRolePermission, AppPermission>();
            db.CodeFirst.InitTables<AppRolePage, AppDepartment, AppPosition, AppProject, AppProjectUser>();
        }

        if (SqliteSchemaAlignment.IsSqlite(db))
        {
            return await SqliteSchemaAlignment.EnsureAppMembershipDomainSchemaAsync(db, cancellationToken);
        }

        return null;
    }

    private static async Task EnsureAppPermissionSeedBackfillAsync(
        ISqlSugarClient db,
        IIdGeneratorProvider idGeneratorProvider,
        string generatorAppId,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AppRole", false)
            || !db.DbMaintenance.IsAnyTable("AppPermission", false)
            || !db.DbMaintenance.IsAnyTable("AppRolePermission", false))
        {
            return;
        }

        var permissionSeeds = AppPermissionSeedCatalog.PermissionSeeds;
        if (permissionSeeds.Count == 0)
        {
            return;
        }

        var roles = await db.Queryable<AppRole>().ToListAsync(cancellationToken);
        var existingPermissions = await db.Queryable<AppPermission>().ToListAsync(cancellationToken);
        if (roles.Count == 0 && existingPermissions.Count == 0)
        {
            return;
        }

        var appScopes = roles
            .Select(role => new AppPermissionScope(role.TenantIdValue, role.AppId))
            .Concat(existingPermissions.Select(permission => new AppPermissionScope(permission.TenantIdValue, permission.AppId)))
            .Distinct()
            .ToArray();
        if (appScopes.Length == 0)
        {
            return;
        }

        var permissionByKey = existingPermissions.ToDictionary(
            permission => BuildAppPermissionKey(permission.TenantIdValue, permission.AppId, permission.Code),
            StringComparer.OrdinalIgnoreCase);
        var newPermissions = new List<AppPermission>();
        var permissionsToUpdate = new List<AppPermission>();

        foreach (var appScope in appScopes)
        {
            var tenantId = new TenantId(appScope.TenantIdValue);
            foreach (var seed in permissionSeeds)
            {
                var permissionKey = BuildAppPermissionKey(appScope.TenantIdValue, appScope.AppId, seed.Code);
                if (permissionByKey.TryGetValue(permissionKey, out var existingPermission))
                {
                    if (!string.Equals(existingPermission.Name, seed.Name, StringComparison.Ordinal)
                        || !string.Equals(existingPermission.Type, seed.Type, StringComparison.Ordinal))
                    {
                        existingPermission.Update(seed.Name, seed.Type, existingPermission.Description);
                        permissionsToUpdate.Add(existingPermission);
                    }

                    continue;
                }

                var permission = new AppPermission(
                    tenantId,
                    appScope.AppId,
                    seed.Name,
                    seed.Code,
                    seed.Type,
                    idGeneratorProvider.NextId(tenantId, generatorAppId));
                newPermissions.Add(permission);
                permissionByKey[permissionKey] = permission;
            }
        }

        if (newPermissions.Count > 0)
        {
            await db.Insertable(newPermissions).ExecuteCommandAsync(cancellationToken);
        }

        if (permissionsToUpdate.Count > 0)
        {
            await db.Updateable(permissionsToUpdate)
                .WhereColumns(permission => new
                {
                    permission.TenantIdValue,
                    permission.AppId,
                    permission.Code
                })
                .UpdateColumns(permission => new
                {
                    permission.Name,
                    permission.Type,
                    permission.Description
                })
                .ExecuteCommandAsync(cancellationToken);
        }

        if (roles.Count == 0)
        {
            return;
        }

        var existingRolePermissions = await db.Queryable<AppRolePermission>().ToListAsync(cancellationToken);
        var existingRolePermissionKeys = existingRolePermissions
            .Select(permission => BuildAppRolePermissionKey(
                permission.TenantIdValue,
                permission.AppId,
                permission.RoleId,
                permission.PermissionCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newRolePermissions = new List<AppRolePermission>();

        foreach (var role in roles)
        {
            var tenantId = new TenantId(role.TenantIdValue);
            var grantedPermissionCodes = AppPermissionSeedCatalog.GetPermissionCodesForRole(role.Code);
            foreach (var permissionCode in grantedPermissionCodes)
            {
                var permissionKey = BuildAppPermissionKey(role.TenantIdValue, role.AppId, permissionCode);
                if (!permissionByKey.ContainsKey(permissionKey))
                {
                    continue;
                }

                var rolePermissionKey = BuildAppRolePermissionKey(
                    role.TenantIdValue,
                    role.AppId,
                    role.Id,
                    permissionCode);
                if (!existingRolePermissionKeys.Add(rolePermissionKey))
                {
                    continue;
                }

                newRolePermissions.Add(new AppRolePermission(
                    tenantId,
                    role.AppId,
                    role.Id,
                    permissionCode,
                    idGeneratorProvider.NextId(tenantId, generatorAppId)));
            }
        }

        if (newRolePermissions.Count > 0)
        {
            await db.Insertable(newRolePermissions).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task EnsureModelConfigSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ModelConfig", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ModelConfig>(
                db,
                "SystemPrompt",
                "Temperature",
                "MaxTokens",
                "TopP",
                "FrequencyPenalty",
                "PresencePenalty",
                "UpdatedAt")
            && !RequiresMissingColumnFix<ModelConfig>(
                db,
                "ModelId",
                "SystemPrompt",
                "EnableStreaming",
                "EnableReasoning",
                "EnableTools",
                "EnableVision",
                "EnableJsonMode",
                "Temperature",
                "MaxTokens",
                "TopP",
                "FrequencyPenalty",
                "PresencePenalty",
                "UpdatedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ModelConfig>(db, cancellationToken);
    }

    private static async Task EnsureAgentSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("Agent", false))
        {
            return;
        }

        if (!RequiresMissingColumnFix<Agent>(db, "DefaultWorkflowId", "DefaultWorkflowName"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<Agent>(db, cancellationToken);
    }

    private static async Task EnsureAssetSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("Asset", false))
        {
            return;
        }

        await AddColumnIfMissingAsync(db, "Asset", "CreatedByUserId", "INTEGER NULL", cancellationToken);
    }

    private static async Task EnsureSystemConfigSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("SystemConfig", false))
        {
            return;
        }

        await AddColumnIfMissingAsync(db, "SystemConfig", "AppId", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "SystemConfig", "GroupName", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "SystemConfig", "IsEncrypted", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, "SystemConfig", "Version", "INTEGER NOT NULL DEFAULT 0", cancellationToken);

        if (RequiresNullableColumnFix<SystemConfig>(db, "Remark", "TargetJson", "AppId", "GroupName"))
        {
            await RebuildTableViaOrmAsync<SystemConfig>(db, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await db.Ado.ExecuteCommandAsync(
            "CREATE INDEX IF NOT EXISTS IX_SystemConfig_Tenant_App_Key ON SystemConfig (TenantIdValue, AppId, ConfigKey)");
    }

    private static async Task EnsureAuthSessionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AuthSession", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<AuthSession>(db, "RevokedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<AuthSession>(db, cancellationToken);
    }

    private static async Task EnsurePermissionAppIdNullableSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var tableName = db.EntityMaintenance.GetTableName<Permission>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<Permission>(db, "AppId"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<Permission>(db, cancellationToken);
    }

    private static async Task EnsureTenantDataSourceSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("TenantDataSource", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<TenantDataSource>(db, "AppId", "LastTestSuccess", "LastTestedAt", "LastTestMessage", "UpdatedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<TenantDataSource>(db, cancellationToken);
    }

    private static async Task EnsureProductizationSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        await EnsureAppManifestSchemaAsync(db, cancellationToken);
        await EnsureAppReleaseSchemaAsync(db, cancellationToken);
        await EnsurePackageArtifactSchemaAsync(db, cancellationToken);
        await EnsureLicenseGrantSchemaAsync(db, cancellationToken);
        await EnsureToolAuthorizationPolicySchemaAsync(db, cancellationToken);
    }

    private static async Task EnsureWorkflowExecutionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("WorkflowExecution", false))
        {
            return;
        }

        if (!RequiresMissingColumnFix<WorkflowExecution>(db, "AppId", "ReleaseId", "RuntimeContextId"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<WorkflowExecution>(db, cancellationToken);
    }

    private static async Task EnsureAiPluginSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AiPlugin", false))
        {
            return;
        }

        await AddColumnIfMissingAsync(db, "AiPlugin", "SourceType", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "AuthType", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "AuthConfigJson", "TEXT NOT NULL DEFAULT '{}'", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "ToolSchemaJson", "TEXT NOT NULL DEFAULT '{}'", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "OpenApiSpecJson", "TEXT NOT NULL DEFAULT '{}'", cancellationToken);
    }

    private static async Task EnsureWorkspacePortalSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        await AddColumnIfMissingAsync(db, "Agent", "WorkspaceId", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiApp", "WorkspaceId", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "WorkflowMeta", "WorkspaceId", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "KnowledgeBase", "WorkspaceId", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiDatabase", "WorkspaceId", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(db, "AiPlugin", "WorkspaceId", "INTEGER NULL", cancellationToken);
        // Coze M5.1：评测任务挂上工作空间归属（nullable，历史任务保留 NULL）
        await AddColumnIfMissingAsync(db, "EvaluationTask", "WorkspaceId", "TEXT NULL", cancellationToken);

        var missingWorkspaceTables =
            !db.DbMaintenance.IsAnyTable("Workspace", false) ||
            !db.DbMaintenance.IsAnyTable("WorkspaceRole", false) ||
            !db.DbMaintenance.IsAnyTable("WorkspaceMember", false) ||
            !db.DbMaintenance.IsAnyTable("WorkspaceResourcePermission", false);
        if (missingWorkspaceTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<Workspace, WorkspaceRole, WorkspaceMember, WorkspaceResourcePermission>();
        }

        // 1→N 模型：Workspace.AppInstanceId / AppKey 改为可空（历史一对一字段，仅作为「默认主应用」回填）
        var requiresWorkspaceNullableFix =
            RequiresNullableColumnFix<Workspace>(db, "AppInstanceId", "AppKey")
            || RequiresWorkspaceLegacyNotNullFix(db);
        if (db.DbMaintenance.IsAnyTable("Workspace", false) && requiresWorkspaceNullableFix)
        {
            await RebuildTableViaOrmAsync<Workspace>(db, cancellationToken);
        }
    }

    private static async Task EnsureAiMemorySchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var missingLongTerm = !db.DbMaintenance.IsAnyTable("LongTermMemory", false);
        if (missingLongTerm)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<LongTermMemory>();
        }

        if (db.DbMaintenance.IsAnyTable("Agent", false))
        {
            await AddColumnIfMissingAsync(db, "Agent", "EnableMemory", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddColumnIfMissingAsync(db, "Agent", "EnableShortTermMemory", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddColumnIfMissingAsync(db, "Agent", "EnableLongTermMemory", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
            await AddColumnIfMissingAsync(db, "Agent", "LongTermMemoryTopK", "INTEGER NOT NULL DEFAULT 3", cancellationToken);
        }
    }

    private static async Task EnsureAgentPublicationSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AgentPublication", false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<AgentPublication>();
            return;
        }

        if (RequiresNullableColumnFix<AgentPublication>(db, "ReleaseNote", "UpdatedAt", "RevokedAt"))
        {
            await RebuildTableViaOrmAsync<AgentPublication>(db, cancellationToken);
        }
    }

    private static async Task EnsureTeamAgentSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var missingTeamAgentTables =
            !db.DbMaintenance.IsAnyTable("TeamAgent", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentPublication", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentTemplate", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentTemplateMember", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentConversation", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentMessage", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentExecution", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentExecutionStep", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentSchemaDraft", false) ||
            !db.DbMaintenance.IsAnyTable("TeamAgentSchemaDraftExecutionAudit", false);

        if (missingTeamAgentTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<TeamAgent, TeamAgentPublication, TeamAgentTemplate, TeamAgentTemplateMember, TeamAgentConversation>();
            db.CodeFirst.InitTables<TeamAgentMessage, TeamAgentExecution, TeamAgentExecutionStepEntity, TeamAgentSchemaDraft, TeamAgentSchemaDraftExecutionAudit>();
            return;
        }

        if (RequiresNullableColumnFix<TeamAgent>(db, "Description", "DefaultEntrySkill", "PublishedAt")
            || RequiresNullableColumnFix<TeamAgentPublication>(db, "ReleaseNote", "RevokedAt")
            || RequiresNullableColumnFix<TeamAgentTemplate>(db, "DefaultEntrySkill")
            || RequiresNullableColumnFix<TeamAgentTemplateMember>(db, "Responsibility", "Alias", "PromptPrefix")
            || RequiresNullableColumnFix<TeamAgentConversation>(db, "Title")
            || RequiresNullableColumnFix<TeamAgentMessage>(db, "Metadata", "MemberName")
            || RequiresNullableColumnFix<TeamAgentExecution>(db, "OutputMessage", "ErrorMessage", "CompletedAt")
            || RequiresNullableColumnFix<TeamAgentExecutionStepEntity>(db, "AgentId", "Alias", "OutputMessage", "ErrorMessage", "CompletedAt")
            || RequiresNullableColumnFix<TeamAgentSchemaDraft>(db, "ConversationId", "AppId", "ConfirmedAt", "DiscardedAt")
            || RequiresNullableColumnFix<TeamAgentSchemaDraftExecutionAudit>(db, "ResourceKey", "ResourceId", "Detail")
            || RequiresMissingColumnFix<TeamAgentSchemaDraft>(db, "CreatedResourcesJson"))
        {
            await RebuildTableViaOrmAsync<TeamAgent>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentPublication>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentTemplate>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentTemplateMember>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentConversation>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentMessage>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentExecution>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentExecutionStepEntity>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentSchemaDraft>(db, cancellationToken);
            await RebuildTableViaOrmAsync<TeamAgentSchemaDraftExecutionAudit>(db, cancellationToken);
        }

    }

    private static async Task EnsureAgentTeamSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var missing =
            !db.DbMaintenance.IsAnyTable("AgentTeamDefinition", false) ||
            !db.DbMaintenance.IsAnyTable("SubAgentDefinition", false) ||
            !db.DbMaintenance.IsAnyTable("OrchestrationNodeDefinition", false) ||
            !db.DbMaintenance.IsAnyTable("TeamVersion", false) ||
            !db.DbMaintenance.IsAnyTable("ExecutionRun", false) ||
            !db.DbMaintenance.IsAnyTable("NodeRun", false);

        if (missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<AgentTeamDefinition, SubAgentDefinition, OrchestrationNodeDefinition>();
            db.CodeFirst.InitTables<TeamVersion, ExecutionRun, NodeRun>();
        }
    }

    private static async Task EnsureTeamAgentTemplateSeedDataAsync(
        ISqlSugarClient db,
        IAppContextAccessor appContextAccessor,
        IIdGeneratorAccessor idGeneratorAccessor,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.TryParse("00000000-0000-0000-0000-000000000001", out var seedTenantId)
            ? seedTenantId
            : Guid.Empty;
        if (tenantId == Guid.Empty || !db.DbMaintenance.IsAnyTable("TeamAgentTemplate", false))
        {
            return;
        }
        using var appContextScope = appContextAccessor.BeginScope(
            CreateSystemContext(appContextAccessor, new TenantId(tenantId)));

        var existingKeys = await db.Queryable<TeamAgentTemplate>()
            .Where(x => x.TenantIdValue == tenantId)
            .Select(x => x.Key)
            .ToListAsync(cancellationToken);
        var keySet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var templates = new[]
        {
            new
            {
                Key = "schema_builder",
                Name = "数据建模团队",
                Description = "面向数据管理的建表协作团队",
                TeamMode = TeamAgentMode.GroupChat,
                CapabilityTagsJson = "[\"schema_builder\",\"knowledge\"]",
                DefaultEntrySkill = "schema_builder",
                Members = new[]
                {
                    new { RoleName = "业务分析 Agent", Responsibility = "拆解业务实体与字段", Alias = "analyst", SortOrder = 1, IsEnabled = true, PromptPrefix = "专注业务建模分析。", CapabilityTagsJson = "[\"analysis\"]" },
                    new { RoleName = "DBA Agent", Responsibility = "给出主键、索引与关系建议", Alias = "dba", SortOrder = 2, IsEnabled = true, PromptPrefix = "专注数据库设计。", CapabilityTagsJson = "[\"schema\"]" },
                    new { RoleName = "权限策略 Agent", Responsibility = "生成字段权限建议", Alias = "security", SortOrder = 3, IsEnabled = true, PromptPrefix = "专注权限与隔离设计。", CapabilityTagsJson = "[\"security\"]" }
                }
            },
            new
            {
                Key = "document_review",
                Name = "文档审查团队",
                Description = "用于文档审核与风险识别",
                TeamMode = TeamAgentMode.Workflow,
                CapabilityTagsJson = "[\"knowledge\"]",
                DefaultEntrySkill = "chat",
                Members = new[]
                {
                    new { RoleName = "审查 Agent", Responsibility = "进行文档审查", Alias = "reviewer", SortOrder = 1, IsEnabled = true, PromptPrefix = "专注文档审查与问题归纳。", CapabilityTagsJson = "[\"review\"]" }
                }
            },
            new
            {
                Key = "security_analysis",
                Name = "安全分析团队",
                Description = "用于漏洞分析与汇总",
                TeamMode = TeamAgentMode.Workflow,
                CapabilityTagsJson = "[\"ops\"]",
                DefaultEntrySkill = "ops",
                Members = new[]
                {
                    new { RoleName = "安全 Agent", Responsibility = "分析安全风险", Alias = "security", SortOrder = 1, IsEnabled = true, PromptPrefix = "专注漏洞与风险分析。", CapabilityTagsJson = "[\"security\"]" }
                }
            },
            new
            {
                Key = "customer_service",
                Name = "客服协作团队",
                Description = "用于工单分流与回复",
                TeamMode = TeamAgentMode.Handoff,
                CapabilityTagsJson = "[\"chat\"]",
                DefaultEntrySkill = "chat",
                Members = new[]
                {
                    new { RoleName = "客服 Agent", Responsibility = "处理客户请求", Alias = "support", SortOrder = 1, IsEnabled = true, PromptPrefix = "专注客户响应与问题澄清。", CapabilityTagsJson = "[\"support\"]" }
                }
            }
        };

        foreach (var template in templates)
        {
            if (keySet.Contains(template.Key))
            {
                continue;
            }

            var templateId = idGeneratorAccessor.NextId();
            var templateEntity = new TeamAgentTemplate(
                new TenantId(tenantId),
                template.Key,
                template.Name,
                template.Description,
                template.TeamMode,
                template.CapabilityTagsJson,
                template.DefaultEntrySkill,
                true,
                templateId);
            await db.Insertable(templateEntity).ExecuteCommandAsync(cancellationToken);

            var members = template.Members.Select(member => new TeamAgentTemplateMember(
                new TenantId(tenantId),
                templateId,
                member.RoleName,
                member.Responsibility,
                member.Alias,
                member.SortOrder,
                member.IsEnabled,
                member.PromptPrefix,
                member.CapabilityTagsJson,
                idGeneratorAccessor.NextId())).ToList();
            if (members.Count > 0)
            {
                await db.Insertable(members).ExecuteCommandAsync(cancellationToken);
            }
        }
    }

    private static async Task EnsureDefaultWorkspacesAsync(
        IServiceProvider serviceProvider,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("Workspace", false)
            || !db.DbMaintenance.IsAnyTable("WorkspaceRole", false)
            || !db.DbMaintenance.IsAnyTable("WorkspaceMember", false))
        {
            return;
        }

        var idGeneratorAccessor = serviceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var appContextAccessor = serviceProvider.GetRequiredService<IAppContextAccessor>();
        var tenantIds = await db.Queryable<UserAccount>()
            .Where(x => x.IsActive)
            .Select(x => x.TenantIdValue)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var tenantGuid in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenantId = new TenantId(tenantGuid);
            using var appContextScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, tenantId));
            var activeUsers = await db.Queryable<UserAccount>()
                .Where(x => x.TenantIdValue == tenantGuid && x.IsActive)
                .OrderBy(x => x.Id, OrderByType.Asc)
                .ToListAsync(cancellationToken);
            if (activeUsers.Count == 0)
            {
                continue;
            }

            var workspace = await db.Queryable<Workspace>()
                .Where(x => x.TenantIdValue == tenantGuid && !x.IsArchived)
                .OrderBy(x => x.CreatedAt, OrderByType.Asc)
                .FirstAsync(cancellationToken);
            if (workspace is null)
            {
                var primaryApp = await db.Queryable<AppManifest>()
                    .Where(x => x.TenantIdValue == tenantGuid)
                    .OrderBy(x => x.Id, OrderByType.Asc)
                    .FirstAsync(cancellationToken);
                if (primaryApp is null)
                {
                    continue;
                }

                var ownerUserId = activeUsers.FirstOrDefault(x => x.IsPlatformAdmin)?.Id
                    ?? activeUsers[0].Id;
#pragma warning disable CS0618 // 历史一对一构造保留给本迁移路径专用
                workspace = new Workspace(
                    tenantId,
                    string.IsNullOrWhiteSpace(primaryApp.Name) ? "默认工作空间" : $"{primaryApp.Name} 工作空间",
                    "系统迁移生成的默认工作空间",
                    primaryApp.Icon,
                    primaryApp.Id,
                    primaryApp.AppKey,
                    ownerUserId,
                    idGeneratorAccessor.NextId());
#pragma warning restore CS0618
                await db.Insertable(workspace).ExecuteCommandAsync(cancellationToken);
            }

            var roles = await db.Queryable<WorkspaceRole>()
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == workspace.Id)
                .ToListAsync(cancellationToken);
            var roleMap = roles.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

            if (!roleMap.ContainsKey(WorkspaceBuiltInRoleCodes.Owner))
            {
                var ownerRole = new WorkspaceRole(
                    tenantId,
                    workspace.Id,
                    WorkspaceBuiltInRoleCodes.Owner,
                    "拥有者",
                    JsonSerializer.Serialize(new[]
                    {
                        WorkspacePermissionActions.View,
                        WorkspacePermissionActions.Edit,
                        WorkspacePermissionActions.Publish,
                        WorkspacePermissionActions.Delete,
                        WorkspacePermissionActions.ManagePermission
                    }),
                    true,
                    idGeneratorAccessor.NextId());
                await db.Insertable(ownerRole).ExecuteCommandAsync(cancellationToken);
                roleMap[ownerRole.Code] = ownerRole;
            }

            if (!roleMap.ContainsKey(WorkspaceBuiltInRoleCodes.Admin))
            {
                var adminRole = new WorkspaceRole(
                    tenantId,
                    workspace.Id,
                    WorkspaceBuiltInRoleCodes.Admin,
                    "管理员",
                    JsonSerializer.Serialize(new[]
                    {
                        WorkspacePermissionActions.View,
                        WorkspacePermissionActions.Edit,
                        WorkspacePermissionActions.Publish,
                        WorkspacePermissionActions.Delete,
                        WorkspacePermissionActions.ManagePermission
                    }),
                    true,
                    idGeneratorAccessor.NextId());
                await db.Insertable(adminRole).ExecuteCommandAsync(cancellationToken);
                roleMap[adminRole.Code] = adminRole;
            }

            if (!roleMap.ContainsKey(WorkspaceBuiltInRoleCodes.Member))
            {
                var memberRole = new WorkspaceRole(
                    tenantId,
                    workspace.Id,
                    WorkspaceBuiltInRoleCodes.Member,
                    "成员",
                    JsonSerializer.Serialize(new[]
                    {
                        WorkspacePermissionActions.View
                    }),
                    true,
                    idGeneratorAccessor.NextId());
                await db.Insertable(memberRole).ExecuteCommandAsync(cancellationToken);
                roleMap[memberRole.Code] = memberRole;
            }

            var memberRoleId = roleMap[WorkspaceBuiltInRoleCodes.Member].Id;
            var ownerRoleId = roleMap[WorkspaceBuiltInRoleCodes.Owner].Id;
            var existingMemberUserIds = await db.Queryable<WorkspaceMember>()
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == workspace.Id)
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);
            var memberIdSet = existingMemberUserIds.ToHashSet();
            var ownerUserIdValue = activeUsers.FirstOrDefault(x => x.IsPlatformAdmin)?.Id
                ?? activeUsers[0].Id;
            var workspaceMembers = new List<WorkspaceMember>();
            foreach (var user in activeUsers)
            {
                if (memberIdSet.Contains(user.Id))
                {
                    continue;
                }

                workspaceMembers.Add(new WorkspaceMember(
                    tenantId,
                    workspace.Id,
                    user.Id,
                    user.Id == ownerUserIdValue ? ownerRoleId : memberRoleId,
                    ownerUserIdValue,
                    idGeneratorAccessor.NextId()));
            }

            if (workspaceMembers.Count > 0)
            {
                await db.Insertable(workspaceMembers.ToArray()).ExecuteCommandAsync(cancellationToken);
            }

            await db.Updateable<Agent>()
                .SetColumns(x => x.WorkspaceId == workspace.Id)
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == null)
                .ExecuteCommandAsync(cancellationToken);
            await db.Updateable<AiApp>()
                .SetColumns(x => x.WorkspaceId == workspace.Id)
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == null)
                .ExecuteCommandAsync(cancellationToken);
            await db.Updateable<WorkflowMeta>()
                .SetColumns(x => x.WorkspaceId == workspace.Id)
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == null)
                .ExecuteCommandAsync(cancellationToken);
            await db.Updateable<KnowledgeBase>()
                .SetColumns(x => x.WorkspaceId == workspace.Id)
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == null)
                .ExecuteCommandAsync(cancellationToken);
            await db.Updateable<AiDatabase>()
                .SetColumns(x => x.WorkspaceId == workspace.Id)
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == null)
                .ExecuteCommandAsync(cancellationToken);
            await db.Updateable<AiPlugin>()
                .SetColumns(x => x.WorkspaceId == workspace.Id)
                .Where(x => x.TenantIdValue == tenantGuid && x.WorkspaceId == null)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task EnsureAppManifestSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AppManifest", false)) return;
        // 1→N 模型：WorkspaceId 是新加的可空列；先补列，再判断 nullable 修复。
        await AddColumnIfMissingAsync(db, "AppManifest", "WorkspaceId", "INTEGER NULL", cancellationToken);
        if (!RequiresNullableColumnFix<AppManifest>(db, "DataSourceId", "PublishedBy", "PublishedAt", "WorkspaceId")) return;
        await RebuildTableViaOrmAsync<AppManifest>(db, cancellationToken);
    }

    private static async Task EnsureAppReleaseSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AppRelease", false)) return;
        if (!RequiresNullableColumnFix<AppRelease>(db, "RollbackPointId")) return;
        await RebuildTableViaOrmAsync<AppRelease>(db, cancellationToken);
    }

    private static async Task EnsurePackageArtifactSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("PackageArtifact", false)) return;
        if (!RequiresNullableColumnFix<PackageArtifact>(db, "ExportedBy", "ExportedAt", "ImportedBy", "ImportedAt")) return;
        await RebuildTableViaOrmAsync<PackageArtifact>(db, cancellationToken);
    }

    private static async Task EnsureLicenseGrantSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("LicenseGrant", false)) return;
        if (!RequiresNullableColumnFix<LicenseGrant>(db, "ExpiresAt")) return;
        await RebuildTableViaOrmAsync<LicenseGrant>(db, cancellationToken);
    }

    private static async Task EnsureToolAuthorizationPolicySchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ToolAuthorizationPolicy", false)) return;
        if (!RequiresNullableColumnFix<ToolAuthorizationPolicy>(db, "ApprovalFlowId")) return;
        await RebuildTableViaOrmAsync<ToolAuthorizationPolicy>(db, cancellationToken);
    }

    private static async Task EnsureApprovalSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        await EnsureApprovalFlowDefinitionSchemaAsync(db, cancellationToken);
        await EnsureApprovalProcessInstanceSchemaAsync(db, cancellationToken);
        await EnsureApprovalTaskSchemaAsync(db, cancellationToken);
        await EnsureApprovalHistoryEventSchemaAsync(db, cancellationToken);
        await EnsureApprovalNodeExecutionSchemaAsync(db, cancellationToken);
        await EnsureApprovalCopyRecordSchemaAsync(db, cancellationToken);
    }

    private static async Task EnsureApprovalFlowDefinitionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalFlowDefinition", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalFlowDefinition>(db, "PublishedAt", "PublishedByUserId", "VisibilityScopeJson", "Category", "Description"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalFlowDefinition>(db, cancellationToken);
    }

    private static async Task EnsureApprovalProcessInstanceSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalProcessInstance", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalProcessInstance>(db, "DataJson", "EndedAt", "CurrentNodeId", "ParentInstanceId", "Priority", "InstanceNo", "CurrentNodeName"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalProcessInstance>(db, cancellationToken);
    }

    private static async Task EnsureApprovalTaskSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalTask", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalTask>(db, "DecisionByUserId", "DecisionAt", "Comment", "OriginalAssigneeValue", "Weight", "ParentTaskId", "DelegatorUserId", "ViewedAt", "TaskType"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalTask>(db, cancellationToken);
    }

    private static async Task EnsureApprovalHistoryEventSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalHistoryEvent", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalHistoryEvent>(db, "FromNode", "ToNode", "PayloadJson", "ActorUserId"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalHistoryEvent>(db, cancellationToken);
    }

    private static async Task EnsureApprovalNodeExecutionSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalNodeExecution", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalNodeExecution>(db, "CompletedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalNodeExecution>(db, cancellationToken);
    }

    private static async Task EnsureApprovalCopyRecordSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("ApprovalCopyRecord", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<ApprovalCopyRecord>(db, "ReadAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<ApprovalCopyRecord>(db, cancellationToken);
    }

    private static Task RebuildTableViaOrmAsync<TEntity>(ISqlSugarClient db, CancellationToken cancellationToken)
        where TEntity : class, new()
        => SqliteSchemaAlignment.RebuildTableViaOrmAsync<TEntity>(db, cancellationToken);

    private static bool RequiresNullableColumnFix<TEntity>(ISqlSugarClient db, params string[] columnNames)
        where TEntity : class, new()
        => SqliteSchemaAlignment.RequiresNullableColumnFix<TEntity>(db, columnNames);

    private static bool RequiresMissingColumnFix<TEntity>(ISqlSugarClient db, params string[] requiredColumnNames)
        where TEntity : class, new()
        => SqliteSchemaAlignment.RequiresMissingColumnFix<TEntity>(db, requiredColumnNames);

    private static bool RequiresWorkspaceLegacyNotNullFix(ISqlSugarClient db)
    {
        if (!db.DbMaintenance.IsAnyTable("Workspace", false))
        {
            return false;
        }

        var ddl = db.Ado.GetString(
            """
            SELECT sql
            FROM sqlite_master
            WHERE lower(type) = 'table'
              AND lower(name) = 'workspace'
            LIMIT 1;
            """);
        if (string.IsNullOrWhiteSpace(ddl))
        {
            return false;
        }

        return ContainsNotNullColumn(ddl, "AppInstanceId")
               || ContainsNotNullColumn(ddl, "AppKey");
    }

    private static bool ContainsNotNullColumn(string ddl, string columnName)
    {
        var pattern = $@"(?is)(?:^|,)\s*(?:\[{Regex.Escape(columnName)}\]|`{Regex.Escape(columnName)}`|""{Regex.Escape(columnName)}""|{Regex.Escape(columnName)})\b[^,]*\bNOT\s+NULL\b";
        return Regex.IsMatch(ddl, pattern, RegexOptions.CultureInvariant);
    }

    private static async Task AddColumnIfMissingAsync(
        ISqlSugarClient db,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        var hasColumn = columns.Any(c => string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
        if (hasColumn)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await db.Ado.ExecuteCommandAsync(
            $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
    }

    private static async Task EnsureRefreshTokenSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("RefreshToken", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<RefreshToken>(db, "RevokedAt", "ReplacedById"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<RefreshToken>(db, cancellationToken);
    }

    private static async Task EnsureAiAppSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("AiApp", false))
        {
            return;
        }

        if (!RequiresNullableColumnFix<AiApp>(
                db,
                "WorkspaceId",
                "AgentId",
                "WorkflowId",
                "PrimaryWorkflowId",
                "EntryConversationTemplateId",
                "PromptTemplateId",
                "UpdatedAt",
                "PublishedAt"))
        {
            return;
        }

        await RebuildTableViaOrmAsync<AiApp>(db, cancellationToken);
    }

    private async Task EnsureBuiltInSystemConfigsAsync(
        SystemConfigRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var seeds = new (string Key, string Value, string Name, string GroupName, string ConfigType, bool IsEncrypted, string? Remark)[]
        {
            // FileStorage
            ("FileStorage:Provider", "local", "存储提供商", "FileStorage", "Text", false, "可选值: local|minio|oss"),
            ("FileStorage:BasePath", "uploads", "本地存储根目录", "FileStorage", "Text", false, "仅 Provider=local 时生效"),
            ("FileStorage:MaxFileSizeBytes", "10485760", "最大文件大小(字节)", "FileStorage", "Number", false, "默认 10MB"),
            ("FileStorage:AllowedExtensions", "[\".jpg\",\".jpeg\",\".png\",\".gif\",\".webp\",\".pdf\",\".xlsx\",\".xls\",\".docx\",\".doc\",\".txt\",\".csv\",\".zip\"]", "允许扩展名", "FileStorage", "Json", false, "上传白名单"),
            ("FileStorage:Minio:Endpoint", "http://127.0.0.1:9000", "MinIO 服务地址", "FileStorage", "Text", false, "仅 Provider=minio 生效"),
            ("FileStorage:Minio:AccessKey", string.Empty, "MinIO AccessKey", "FileStorage", "Text", true, "敏感项，建议通过界面维护"),
            ("FileStorage:Minio:SecretKey", string.Empty, "MinIO SecretKey", "FileStorage", "Text", true, "敏感项，建议通过界面维护"),
            ("FileStorage:Minio:BucketName", "atlas-files", "MinIO Bucket", "FileStorage", "Text", false, null),
            ("FileStorage:Oss:Endpoint", "https://oss-cn-hangzhou.aliyuncs.com", "OSS Endpoint", "FileStorage", "Text", false, "仅 Provider=oss 生效"),
            ("FileStorage:Oss:AccessKeyId", string.Empty, "OSS AccessKeyId", "FileStorage", "Text", true, "敏感项，建议通过界面维护"),
            ("FileStorage:Oss:AccessKeySecret", string.Empty, "OSS AccessKeySecret", "FileStorage", "Text", true, "敏感项，建议通过界面维护"),
            ("FileStorage:Oss:BucketName", "atlas-files", "OSS Bucket", "FileStorage", "Text", false, null),

            // Security
            ("Security:PasswordPolicy:MinLength", "8", "密码最小长度", "Security", "Number", false, "密码策略"),
            ("Security:PasswordPolicy:RequireUppercase", "true", "要求大写字母", "Security", "Boolean", false, "密码策略"),
            ("Security:PasswordPolicy:RequireLowercase", "true", "要求小写字母", "Security", "Boolean", false, "密码策略"),
            ("Security:PasswordPolicy:RequireDigit", "true", "要求数字", "Security", "Boolean", false, "密码策略"),
            ("Security:PasswordPolicy:RequireNonAlphanumeric", "true", "要求特殊字符", "Security", "Boolean", false, "密码策略"),
            ("Security:PasswordPolicy:ExpirationDays", "90", "密码过期天数", "Security", "Number", false, "0 表示不过期"),
            ("Security:LockoutPolicy:MaxFailedAttempts", "5", "最大失败次数", "Security", "Number", false, null),
            ("Security:LockoutPolicy:LockoutMinutes", "15", "锁定时长(分钟)", "Security", "Number", false, null),
            ("Security:LockoutPolicy:AutoUnlockMinutes", "30", "自动解锁时长(分钟)", "Security", "Number", false, null),
            ("Security:MaxConcurrentSessions", "5", "最大并发会话数", "Security", "Number", false, "0 表示不限制"),
            ("Security:CaptchaThreshold", "3", "验证码触发阈值", "Security", "Number", false, "0 表示禁用"),

            // AI Platform
            ("ai.enable-platform", "true", "启用 AI 平台", "AiPlatform", "Boolean", false, null),
            ("ai.enable-open-platform", "true", "启用开放平台", "AiPlatform", "Boolean", false, null),
            ("ai.enable-code-sandbox", "false", "启用代码沙箱", "AiPlatform", "Boolean", false, null),
            ("ai.enable-marketplace", "true", "启用 AI 市场", "AiPlatform", "Boolean", false, null),
            ("ai.enable-content-moderation", "true", "启用内容审核", "AiPlatform", "Boolean", false, null),
            ("ai.max-daily-tokens-per-user", "500000", "单用户每日 Token 上限", "AiPlatform", "Number", false, null),
            ("AiPlatform:DefaultProvider", "openai", "默认 LLM 提供商", "AiPlatform", "Text", false, null),
            ("AiPlatform:Embedding:Model", "text-embedding-3-small", "Embedding 模型", "AiPlatform", "Text", false, null),
            ("AiPlatform:Providers:openai:ApiKey", string.Empty, "OpenAI API Key", "AiPlatform", "Text", true, "敏感项，建议通过界面维护"),

            // System Switch
            ("sys.account.register", "false", "开放自助注册", "SystemSwitch", "Boolean", false, "注册开关"),
            ("sys.maintenance.mode", "false", "维护模式", "SystemSwitch", "Boolean", false, null),
            ("Oidc:Enabled", "false", "启用 SSO/OIDC", "SystemSwitch", "Boolean", false, null),
            ("CodeExecution:Enabled", "false", "启用代码执行", "SystemSwitch", "Boolean", false, null),

            // Backward compatibility keys
            ("security.password.minLength", "8", "密码最小长度(兼容键)", "Legacy", "Number", false, "旧版本兼容键"),
            ("security.lockout.maxFailedAttempts", "5", "最大失败次数(兼容键)", "Legacy", "Number", false, "旧版本兼容键"),
            ("security.lockout.lockMinutes", "15", "锁定时长(分钟)(兼容键)", "Legacy", "Number", false, "旧版本兼容键")
        };

        var seedKeys = seeds.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await repository.GetByKeysAsync(tenantId, seedKeys, cancellationToken);
        var existingKeys = existing
            .Select(x => x.ConfigKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inserts = new List<SystemConfig>();

        foreach (var seed in seeds)
        {
            if (existingKeys.Contains(seed.Key))
            {
                continue;
            }

            var entity = new SystemConfig(
                tenantId,
                seed.Key,
                seed.Value,
                seed.Name,
                true,
                idGeneratorAccessor.NextId(),
                seed.ConfigType,
                appId: null,
                seed.GroupName,
                seed.IsEncrypted,
                version: 0);
            entity.Update(seed.Value, seed.Name, seed.Remark, seed.GroupName, seed.IsEncrypted);
            inserts.Add(entity);
        }

        if (inserts.Count > 0)
        {
            await repository.AddRangeAsync(inserts, cancellationToken);
        }
    }

    /// <summary>平台种子角色与权限码的映射（等保最小化授权）；未列出的引导角色码按租户管理员子集处理（不含 system:admin）。</summary>
    private static IEnumerable<string> EnumerateBootstrapRoleSeedPermissionCodes(
        string roleCode,
        IReadOnlyDictionary<string, long> permissionIdMap)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            yield break;
        }

        if (comparer.Equals(roleCode, "SuperAdmin"))
        {
            foreach (var code in permissionIdMap.Keys)
            {
                yield return code;
            }

            yield break;
        }

        if (comparer.Equals(roleCode, "Admin") || comparer.Equals(roleCode, "TenantAdmin"))
        {
            foreach (var code in permissionIdMap.Keys)
            {
                if (!comparer.Equals(code, PermissionCodes.SystemAdmin))
                {
                    yield return code;
                }
            }

            yield break;
        }

        if (comparer.Equals(roleCode, "SecurityAdmin"))
        {
            foreach (var code in SecurityAdminSeedPermissionCodes)
            {
                if (permissionIdMap.ContainsKey(code))
                {
                    yield return code;
                }
            }

            yield break;
        }

        if (comparer.Equals(roleCode, "AuditAdmin") || comparer.Equals(roleCode, "Auditor"))
        {
            foreach (var code in AuditAdminSeedPermissionCodes)
            {
                if (permissionIdMap.ContainsKey(code))
                {
                    yield return code;
                }
            }

            yield break;
        }

        if (comparer.Equals(roleCode, "AssetAdmin"))
        {
            foreach (var code in AssetAdminSeedPermissionCodes)
            {
                if (permissionIdMap.ContainsKey(code))
                {
                    yield return code;
                }
            }

            yield break;
        }

        if (comparer.Equals(roleCode, "ApprovalAdmin"))
        {
            foreach (var code in ApprovalAdminSeedPermissionCodes)
            {
                if (permissionIdMap.ContainsKey(code))
                {
                    yield return code;
                }
            }

            yield break;
        }

        foreach (var code in permissionIdMap.Keys)
        {
            if (!comparer.Equals(code, PermissionCodes.SystemAdmin))
            {
                yield return code;
            }
        }
    }

    private static readonly string[] SecurityAdminSeedPermissionCodes =
    [
        PermissionCodes.UsersView,
        PermissionCodes.UsersCreate,
        PermissionCodes.UsersUpdate,
        PermissionCodes.UsersDelete,
        PermissionCodes.UsersAssignRoles,
        PermissionCodes.UsersAssignDepartments,
        PermissionCodes.UsersAssignPositions,
        PermissionCodes.RolesView,
        PermissionCodes.RolesCreate,
        PermissionCodes.RolesUpdate,
        PermissionCodes.RolesDelete,
        PermissionCodes.RolesAssignPermissions,
        PermissionCodes.RolesAssignMenus,
        PermissionCodes.PermissionsView,
        PermissionCodes.PermissionsCreate,
        PermissionCodes.PermissionsUpdate,
        PermissionCodes.DepartmentsView,
        PermissionCodes.DepartmentsAll,
        PermissionCodes.DepartmentsCreate,
        PermissionCodes.DepartmentsUpdate,
        PermissionCodes.DepartmentsDelete,
        PermissionCodes.PositionsView,
        PermissionCodes.PositionsCreate,
        PermissionCodes.PositionsUpdate,
        PermissionCodes.PositionsDelete,
        PermissionCodes.MenusView,
        PermissionCodes.MenusAll,
        PermissionCodes.MenusCreate,
        PermissionCodes.MenusUpdate,
        PermissionCodes.MenusDelete,
        PermissionCodes.DataScopeManage
    ];

    private static readonly string[] AuditAdminSeedPermissionCodes =
    [
        PermissionCodes.AuditView,
        PermissionCodes.LoginLogView
    ];

    private static readonly string[] AssetAdminSeedPermissionCodes =
    [
        PermissionCodes.AssetsView,
        PermissionCodes.AssetsCreate,
        PermissionCodes.FileUpload,
        PermissionCodes.FileDownload,
        PermissionCodes.FileDelete
    ];

    private static readonly string[] ApprovalAdminSeedPermissionCodes =
    [
        PermissionCodes.WorkflowDesign,
        PermissionCodes.WorkflowView,
        PermissionCodes.ApprovalFlowView,
        PermissionCodes.ApprovalFlowManage,
        PermissionCodes.ApprovalFlowCreate,
        PermissionCodes.ApprovalFlowUpdate,
        PermissionCodes.ApprovalFlowPublish,
        PermissionCodes.ApprovalFlowDelete,
        PermissionCodes.ApprovalFlowDisable
    ];

    private static async Task EnsureBootstrapRoleDataScopesAsync(
        IRoleRepository roleRepository,
        IReadOnlyDictionary<string, Role> roleMap,
        IReadOnlyList<string> allRoleCodes,
        CancellationToken cancellationToken)
    {
        foreach (var roleCode in allRoleCodes)
        {
            if (!roleMap.TryGetValue(roleCode, out var role))
            {
                continue;
            }

            var expected = string.Equals(roleCode, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                ? DataScopeType.All
                : DataScopeType.CurrentTenant;

            if (role.DataScope == expected)
            {
                continue;
            }

            role.SetDataScope(expected);
            await roleRepository.UpdateAsync(role, cancellationToken);
        }
    }

    private IReadOnlyList<SetupDepartmentSeed> ResolveDepartmentSeeds()
    {
        if (_activeBootstrapParams is not null)
        {
            return _activeBootstrapParams.InitialDepartments
                .Where(seed => !string.IsNullOrWhiteSpace(seed.Name) && !string.IsNullOrWhiteSpace(seed.Code))
                .ToArray();
        }

        return [];
    }

    private IReadOnlyList<ResolvedPositionSeed> ResolvePositionSeeds()
    {
        if (_activeBootstrapParams is not null)
        {
            return _activeBootstrapParams.InitialPositions
                .Where(seed => !string.IsNullOrWhiteSpace(seed.Name) && !string.IsNullOrWhiteSpace(seed.Code))
                .Select(seed => new ResolvedPositionSeed(
                    seed.Name,
                    seed.Code,
                    seed.Description ?? string.Empty,
                    false,
                    seed.SortOrder))
                .ToArray();
        }

        return [];
    }

    private sealed class LegacyAppBindingProjection
    {
        public Guid TenantIdValue { get; set; }

        public long TenantAppInstanceId { get; set; }

        public long DataSourceId { get; set; }

        public long CreatedBy { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class AppBindingHealthProjection
    {
        public Guid TenantIdValue { get; set; }

        public long AppInstanceId { get; set; }

        public long? DataSourceId { get; set; }

        public long UpdatedBy { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class DataSourceHealthProjection
    {
        public string TenantIdValue { get; set; } = string.Empty;

        public long DataSourceId { get; set; }

        public bool IsActive { get; set; }
    }

    private static string BuildAppPermissionKey(Guid tenantIdValue, long appId, string permissionCode)
        => $"{tenantIdValue:N}:{appId}:{permissionCode}";

    private static string BuildAppRolePermissionKey(Guid tenantIdValue, long appId, long roleId, string permissionCode)
        => $"{tenantIdValue:N}:{appId}:{roleId}:{permissionCode}";

    private sealed record ResolvedPositionSeed(
        string Name,
        string Code,
        string Description,
        bool IsSystem,
        int SortOrder);

    private readonly record struct AppPermissionScope(Guid TenantIdValue, long AppId);
}
