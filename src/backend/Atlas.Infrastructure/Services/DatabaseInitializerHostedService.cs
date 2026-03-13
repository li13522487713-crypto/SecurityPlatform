using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Application.Identity;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Alert.Entities;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Assets.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Domain.Plugins;
using Atlas.Domain.Events;
using Atlas.Domain.Workflow.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BootstrapAdminOptions _bootstrapOptions;
    private readonly PasswordPolicyOptions _passwordPolicy;
    private readonly DatabaseEncryptionOptions _encryptionOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    public DatabaseInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BootstrapAdminOptions> bootstrapOptions,
        IOptions<PasswordPolicyOptions> passwordPolicy,
        IOptions<DatabaseEncryptionOptions> encryptionOptions,
        IHostEnvironment environment,
        ILogger<DatabaseInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _bootstrapOptions = bootstrapOptions.Value;
        _passwordPolicy = passwordPolicy.Value;
        _encryptionOptions = encryptionOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_encryptionOptions.Enabled && string.IsNullOrWhiteSpace(_encryptionOptions.Key))
        {
            throw new InvalidOperationException("已启用数据库加密但未配置密钥。");
        }

        using var scope = _scopeFactory.CreateScope();
        var appContextAccessor = scope.ServiceProvider.GetRequiredService<IAppContextAccessor>();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        await EnsureAuthSessionSchemaAsync(db, cancellationToken);
        await EnsureRefreshTokenSchemaAsync(db, cancellationToken);
        db.CodeFirst.InitTables(
        typeof(UserAccount),
        typeof(Role),
        typeof(Permission),
        typeof(UserRole),
        typeof(RolePermission),
        typeof(RoleDept),
        typeof(Department),
        typeof(Position),
        typeof(UserDepartment),
        typeof(UserPosition),
        typeof(PasswordHistory),
            typeof(Menu),
            typeof(AppConfig),
            typeof(Project),
            typeof(ProjectUser),
            typeof(ProjectDepartment),
            typeof(ProjectPosition),
            typeof(RoleMenu),
            typeof(TableView),
            typeof(UserTableViewDefault),
            typeof(IdempotencyRecord),
            typeof(AuditRecord),
            typeof(Asset),
            typeof(AlertRecord),
            typeof(ModelConfig),
            typeof(Agent),
            typeof(AgentKnowledgeLink),
            typeof(Conversation),
            typeof(ChatMessage),
            typeof(AuthSession),
            typeof(RefreshToken),
            typeof(ApprovalFlowDefinition),
            typeof(ApprovalFlowDefinitionVersion),
            typeof(ApprovalWritebackFailure),
            typeof(ApprovalProcessInstance),
            typeof(ApprovalTask),
            typeof(ApprovalHistoryEvent),
            typeof(ApprovalDepartmentLeader),
            typeof(ApprovalProcessVariable),
            typeof(ApprovalTaskTransfer),
            typeof(ApprovalTaskAssigneeChange),
            typeof(ApprovalNodeExecution),
            typeof(ApprovalOperationRecord),
            typeof(ApprovalFlowButtonConfig),
            typeof(ApprovalTimeoutReminder),
            typeof(ApprovalAgentConfig),
            typeof(ApprovalCopyRecord),
            typeof(ApprovalExternalCallbackRecord),
            typeof(ApprovalParallelToken),
            typeof(ApprovalTimerJob),
            typeof(ApprovalTriggerJob),
            typeof(DynamicTable),
            typeof(DynamicField),
            typeof(DynamicIndex),
            typeof(DynamicRelation),
            typeof(FieldPermission),
            typeof(MigrationRecord),
            typeof(LowCodeApp),
            typeof(AppEntityAlias),
            typeof(LowCodePage),
            typeof(LowCodePageVersion),
            typeof(LowCodeEnvironment),
            typeof(FormDefinition),
            typeof(DashboardDefinition),
            typeof(ReportDefinition),
            typeof(DataSourceDefinition),
            // Workflow entities
            typeof(PersistedWorkflow),
            typeof(PersistedExecutionPointer),
            typeof(PersistedEvent),
            typeof(PersistedSubscription),
            // System module
            typeof(DictType),
            typeof(DictData),
            typeof(SystemConfig),
            typeof(LoginLog),
            typeof(Notification),
            typeof(UserNotification),
            typeof(FileRecord),
            typeof(TenantDataSource),
            // Low code module (types already registered above: LowCodeApp, AppEntityAlias, LowCodePage, FormDefinition)
            typeof(LowCodeAppVersion),
            typeof(FormDefinitionVersion),
            // Events / Outbox
            typeof(OutboxMessage),
            // Plugin configuration
            typeof(PluginConfig),
            // Plugin market
            typeof(PluginMarketEntry),
            typeof(PluginMarketVersion),
            // Component templates
            typeof(Atlas.Domain.Templates.ComponentTemplate),
            // Webhooks
            typeof(Atlas.Domain.Integration.WebhookSubscription),
            typeof(Atlas.Domain.Integration.WebhookDeliveryLog),
            // API Connectors
            typeof(Atlas.Domain.Integration.ApiConnector),
            typeof(Atlas.Domain.Integration.ApiConnectorOperation),
            // Integration API Keys
            typeof(Atlas.Domain.Integration.IntegrationApiKey),
            // Message Queue
            typeof(Atlas.Domain.Messaging.QueueMessage),
            // Saga
            typeof(Atlas.Domain.Saga.SagaInstance),
            // Event Subscriptions
            typeof(Atlas.Domain.Events.EventSubscription),
            // License
            typeof(Atlas.Domain.License.LicenseRecord),
            // Productization
            typeof(AppManifest),
            typeof(AppRelease),
            typeof(RuntimeRoute),
            typeof(PackageArtifact),
            typeof(LicenseGrant),
            typeof(ToolAuthorizationPolicy));
        await EnsureApprovalSchemaAsync(db, cancellationToken);

        // 初始化审批模块种子数据（使用 BootstrapAdmin 的 TenantId）
        if (Guid.TryParse(_bootstrapOptions.TenantId, out var seedTenantGuid))
        {
            var approvalSeedService = scope.ServiceProvider.GetRequiredService<ApprovalSeedDataService>();
            var templateSeedDataService = scope.ServiceProvider.GetRequiredService<TemplateSeedDataService>();
            var seedTenantId = new TenantId(seedTenantGuid);
            using var seedScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, seedTenantId));
            await approvalSeedService.InitializeSeedDataAsync(seedTenantId, cancellationToken);
            await templateSeedDataService.InitializeBuiltInTemplatesAsync(seedTenantId, cancellationToken);
        }

        if (Guid.TryParse(_bootstrapOptions.TenantId, out var appTenantGuid))
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

        if (Guid.TryParse(_bootstrapOptions.TenantId, out var configTenantGuid))
        {
            var configTenantId = new TenantId(configTenantGuid);
            using var configScope = appContextAccessor.BeginScope(CreateSystemContext(appContextAccessor, configTenantId));
            await EnsureBuiltInSystemConfigsAsync(
                scope.ServiceProvider.GetRequiredService<SystemConfigRepository>(),
                scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>(),
                configTenantId,
                cancellationToken);
        }

        // 启动时加载授权证书状态到内存（不阻塞启动）
        await LoadLicenseStatusAsync(scope, cancellationToken);

        if (!_bootstrapOptions.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_bootstrapOptions.Password))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning("未配置BootstrapAdmin密码，已跳过创建默认管理员。");
                return;
            }

            throw new InvalidOperationException("生产环境必须配置BootstrapAdmin密码。");
        }

        if (!PasswordPolicy.IsCompliant(_bootstrapOptions.Password, _passwordPolicy, out var message))
        {
            throw new InvalidOperationException($"BootstrapAdmin密码不符合策略：{message}");
        }

        if (!Guid.TryParse(_bootstrapOptions.TenantId, out var tenantGuid))
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

        var roleSeedDefinitions = new (string Code, string Name, string Description, bool IsSystem)[]
        {
            ("SuperAdmin", "超级管理员", "平台超级管理员（全量权限）", true),
            ("Admin", "系统管理员", "系统运维与平台配置管理员", true),
            ("SecurityAdmin", "安全管理员", "安全策略与告警管理员", false),
            ("AuditAdmin", "审计管理员", "审计日志与合规管理员", false),
            ("AssetAdmin", "资产管理员", "资产台账管理员", false),
            ("ApprovalAdmin", "流程管理员", "审批流配置管理员", false)
        };

        var roleSeedMap = roleSeedDefinitions.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
        var roleCodes = _bootstrapOptions.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!roleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
        {
            roleCodes.Add("SuperAdmin");
        }
        var roleCodesArray = roleCodes.ToArray();

        var roleCodeSet = roleCodesArray.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allRoleCodes = roleSeedDefinitions
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

        var roleIds = roleCodesArray
            .Select(roleCode => roleMap[roleCode].Id)
            .Distinct()
            .ToList();

        var permissionSeeds = new (string Code, string Name, string Type)[]
        {
            (PermissionCodes.SystemAdmin, "System Admin", "Api"),
            (PermissionCodes.WorkflowDesign, "Workflow Designer", "Menu"),
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
            (PermissionCodes.AppsView, "Apps View", "Api"),
            (PermissionCodes.AppsUpdate, "Apps Update", "Api"),
            (PermissionCodes.AppAdmin, "App Admin", "Api"),
            (PermissionCodes.AppUser, "App User", "Api"),
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
            ("AI 平台", "/ai", null, 14, "M", "Layout", "robot", null, null, false, false, "0", "0", null, false),
            ("模型配置", "/settings/ai/model-configs", "/ai", 15, "C", "ai/ModelConfigsPage", "api", PermissionCodes.ModelConfigView, null, false, true, "0", "0", PermissionCodes.ModelConfigView, false),
            ("Agent 管理", "/ai/agents", "/ai", 16, "C", "ai/AgentListPage", "experiment", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, false),
            ("Agent 编辑", "/ai/agents/:id/edit", "/ai", 17, "C", "ai/AgentEditorPage", "edit", PermissionCodes.AgentView, null, false, true, "0", "0", PermissionCodes.AgentView, true),

            ("低代码中心", "/lowcode", null, 15, "M", "Layout", "appstore", null, null, false, false, "0", "0", null, false),
            ("应用管理", "/lowcode/apps", "/lowcode", 16, "C", "lowcode/AppListPage", "appstore-add", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("表单管理", "/lowcode/forms", "/lowcode", 17, "C", "lowcode/FormListPage", "form", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("插件市场", "/lowcode/plugin-market", "/lowcode", 18, "C", "lowcode/PluginMarketPage", "shopping", PermissionCodes.AppsView, null, false, true, "0", "0", PermissionCodes.AppsView, false),
            ("回写监控", "/monitor/writeback-failures", "/lowcode", 19, "C", "lowcode/WritebackMonitorPage", "warning", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),

            ("流程中心", "/process", null, 20, "M", "Layout", "cluster", null, null, false, false, "0", "0", null, false),
            ("流程定义", "/approval/flows", "/process", 21, "C", "ApprovalFlowsPage", "apartment", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("发起审批", "/process/start", "/process", 22, "C", "ApprovalStartPage", "send", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的待办", "/process/inbox", "/process", 23, "C", "ApprovalInboxPage", "schedule", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的已办", "/process/done", "/process", 24, "C", "ApprovalDonePage", "check-circle", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我发起的", "/process/my-requests", "/process", 25, "C", "ApprovalMyRequestsPage", "profile", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("我的抄送", "/process/cc", "/process", 26, "C", "ApprovalCcPage", "mail", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("审批设计器", "/process/designer", "/process", 27, "C", "ApprovalDesignerPage", "branches", PermissionCodes.ApprovalFlowUpdate, null, false, true, "0", "0", PermissionCodes.ApprovalFlowUpdate, true),
            ("审批设计详情", "/process/designer/:id", "/process", 28, "C", "ApprovalDesignerPage", "branches", PermissionCodes.ApprovalFlowUpdate, null, false, true, "0", "0", PermissionCodes.ApprovalFlowUpdate, true),
            ("审批任务详情", "/process/tasks/:id", "/process", 29, "C", "ApprovalTaskDetailPage", "file-search", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, true),
            ("审批实例详情", "/process/instances/:id", "/process", 30, "C", "ApprovalInstanceDetailPage", "file-text", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, true),
            ("流程定义管理", "/process/manage/flows", "/process", 31, "C", "ApprovalFlowManagePage", "control", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("流程实例管理", "/process/manage/instances", "/process", 32, "C", "ApprovalInstanceManagePage", "appstore", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("工作流设计", "/workflow/designer", "/process", 33, "C", "WorkflowDesignerPage", "branches", workflowPermission.Code, null, false, true, "0", "0", workflowPermission.Code, false),

            ("运维监控", "/monitor", null, 35, "M", "Layout", "monitor", null, null, false, false, "0", "0", null, false),
            ("消息队列监控", "/monitor/message-queue", "/monitor", 36, "C", "monitor/MessageQueuePage", "deployment-unit", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),

            ("系统管理", "/system", null, 30, "M", "Layout", "setting", null, null, false, false, "0", "0", null, false),
            ("用户管理", "/settings/org/users", "/system", 31, "C", "system/UsersPage", "user", PermissionCodes.UsersView, null, false, true, "0", "0", PermissionCodes.UsersView, false),
            ("角色管理", "/settings/auth/roles", "/system", 32, "C", "system/RolesPage", "team", PermissionCodes.RolesView, null, false, true, "0", "0", PermissionCodes.RolesView, false),
            ("菜单管理", "/settings/auth/menus", "/system", 33, "C", "system/MenusPage", "menu", PermissionCodes.MenusView, null, false, true, "0", "0", PermissionCodes.MenusView, false),
            ("项目管理", "/settings/projects", "/system", 34, "C", "system/ProjectsPage", "project", PermissionCodes.ProjectsView, null, false, true, "0", "0", PermissionCodes.ProjectsView, false),
            ("数据源管理", "/settings/system/datasources", "/system", 35, "C", "system/TenantDataSourcesPage", "database", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("字典管理", "/settings/system/dict-types", "/system", 36, "C", "system/DictTypesPage", "book", PermissionCodes.DictTypeView, null, false, true, "0", "0", PermissionCodes.DictTypeView, false),
            ("参数配置", "/settings/system/configs", "/system", 37, "C", "system/SystemConfigsPage", "tool", PermissionCodes.ConfigView, null, false, true, "0", "0", PermissionCodes.ConfigView, false),
            ("通知中心", "/system/notifications", "/system", 38, "C", "system/NotificationsPage", "notification", PermissionCodes.NotificationView, null, false, true, "0", "0", PermissionCodes.NotificationView, false),
            ("插件管理", "/settings/system/plugins", "/system", 39, "C", "system/PluginManagePage", "api", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),
            ("Webhook管理", "/settings/system/webhooks", "/system", 40, "C", "system/WebhooksPage", "link", PermissionCodes.SystemAdmin, null, false, true, "0", "0", PermissionCodes.SystemAdmin, false),

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
            ("会话删除", "/ai/conversations:delete", "/ai", 7, "F", null, null, PermissionCodes.ConversationDelete, null, false, false, "0", "0", PermissionCodes.ConversationDelete, true)
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

        var departmentSeeds = new (string Name, string? ParentName, int SortOrder)[]
        {
            ("总部", null, 0),
            ("研发部", "总部", 10),
            ("安全运营部", "总部", 20),
            ("运维部", "总部", 30),
            ("人力资源部", "总部", 40),
            ("财务部", "总部", 50)
        };

        var departmentNameList = departmentSeeds.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existingDepartments = await db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(departmentNameList, x.Name))
            .ToListAsync(cancellationToken);
        var departmentIdMap = existingDepartments.ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var departmentsToInsert = new List<Department>();
        foreach (var seed in departmentSeeds)
        {
            if (departmentIdMap.ContainsKey(seed.Name))
            {
                continue;
            }

            var parentId = 0L;
            if (!string.IsNullOrWhiteSpace(seed.ParentName) &&
                departmentIdMap.TryGetValue(seed.ParentName, out var resolvedParentId))
            {
                parentId = resolvedParentId;
            }

            var department = new Department(tenantId, seed.Name, idGeneratorAccessor.NextId(), parentId, seed.SortOrder);
            departmentsToInsert.Add(department);
            departmentIdMap[seed.Name] = department.Id;
        }

        if (departmentsToInsert.Count > 0)
        {
            await db.Insertable(departmentsToInsert).ExecuteCommandAsync(cancellationToken);
        }

        var positionSeeds = new (string Name, string Code, string Description, bool IsSystem, int SortOrder)[]
        {
            ("安全负责人", "SEC_LEAD", "安全策略与风险管理负责人", true, 10),
            ("安全工程师", "SEC_ENG", "安全工程与检测响应", false, 20),
            ("审计专员", "AUDITOR", "审计与合规执行", false, 30),
            ("资产管理员", "ASSET_ADMIN", "资产台账与生命周期管理", false, 40),
            ("系统管理员", "SYS_ADMIN", "系统配置与运维管理", true, 50),
            ("运维工程师", "OPS_ENG", "平台运维与监控", false, 60)
        };

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

        var permissionIds = permissionIdMap.Values.Distinct().ToArray();
        var existingRolePermissions = await rolePermissionRepository.QueryByRoleIdsAsync(
            tenantId,
            roleIds,
            cancellationToken);
        var existingRolePermissionSet = existingRolePermissions
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        var rolePermissionsToInsert = new List<RolePermission>();
        foreach (var roleId in roleIds)
        {
            foreach (var permissionId in permissionIds)
            {
                if (existingRolePermissionSet.Contains((roleId, permissionId)))
                {
                    continue;
                }

                rolePermissionsToInsert.Add(new RolePermission(tenantId, roleId, permissionId, idGeneratorAccessor.NextId()));
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
            "/lowcode",
            "/lowcode/apps",
            "/lowcode/forms",
            "/lowcode/plugin-market",
            "/monitor/writeback-failures",
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
            "/settings/org/users",
            "/settings/auth/roles",
            "/settings/auth/menus",
            "/settings/projects",
            "/settings/system/datasources",
            "/settings/system/dict-types",
            "/settings/system/configs",
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

        var existing = await userRepository.FindByUsernameAsync(tenantId, _bootstrapOptions.Username, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateRoles(string.Join(',', roleCodes));
            await userRepository.UpdateAsync(existing, cancellationToken);
            await userRoleRepository.DeleteByUserIdAsync(tenantId, existing.Id, cancellationToken);
            await userRoleRepository.AddRangeAsync(
                roleIds.Select(roleId => new UserRole(tenantId, existing.Id, roleId, idGeneratorAccessor.NextId())).ToArray(),
                cancellationToken);
            return;
        }

        var hashed = passwordHasher.HashPassword(_bootstrapOptions.Password);
        var account = new UserAccount(tenantId, _bootstrapOptions.Username, _bootstrapOptions.Username, hashed, idGeneratorAccessor.NextId());
        account.UpdateRoles(string.Join(',', roleCodes));
        account.MarkSystemAccount();
        await userRepository.AddAsync(account, cancellationToken);
        await userRoleRepository.AddRangeAsync(
            roleIds.Select(roleId => new UserRole(tenantId, account.Id, roleId, idGeneratorAccessor.NextId())).ToArray(),
            cancellationToken);
        _logger.LogInformation("已创建BootstrapAdmin账号：{Username}", _bootstrapOptions.Username);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

    private static async Task RebuildTableViaOrmAsync<TEntity>(ISqlSugarClient db, CancellationToken cancellationToken)
        where TEntity : class, new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var data = await db.Queryable<TEntity>().ToListAsync(cancellationToken);
        db.DbMaintenance.DropTable(tableName);
        db.CodeFirst.InitTables<TEntity>();
        if (data.Count > 0)
        {
            await db.Insertable(data).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static bool RequiresNullableColumnFix<TEntity>(ISqlSugarClient db, params string[] columnNames)
        where TEntity : class, new()
    {
        var tableName = db.EntityMaintenance.GetTableName<TEntity>();
        var columns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false);
        foreach (var columnName in columnNames)
        {
            var column = columns.FirstOrDefault(c => string.Equals(c.DbColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (column is not null && !column.IsNullable)
            {
                return true;
            }
        }

        return false;
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

    private async Task EnsureBuiltInSystemConfigsAsync(
        SystemConfigRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var seeds = new (string Key, string Value, string Name, string? Remark)[]
        {
            ("security.password.minLength", "8", "密码最小长度", "内置安全策略参数"),
            ("security.lockout.maxFailedAttempts", "5", "最大失败次数", "内置安全策略参数"),
            ("security.lockout.lockMinutes", "15", "锁定时长(分钟)", "内置安全策略参数"),
            ("sys.account.register", "false", "是否开启自助注册", "内置注册策略参数")
        };

        foreach (var seed in seeds)
        {
            var existing = await repository.FindByKeyAsync(tenantId, seed.Key, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            var entity = new SystemConfig(
                tenantId,
                seed.Key,
                seed.Value,
                seed.Name,
                true,
                idGeneratorAccessor.NextId());
            entity.Update(seed.Value, seed.Name, seed.Remark);
            await repository.AddAsync(entity, cancellationToken);
        }
    }
}




