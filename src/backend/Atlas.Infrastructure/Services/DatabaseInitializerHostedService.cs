using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Application.Identity;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Alert.Entities;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Assets.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Domain.Workflow.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Data;

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
            typeof(AuthSession),
            typeof(RefreshToken),
            typeof(ApprovalFlowDefinition),
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
            typeof(ApprovalExternalCallbackRecord),
            typeof(ApprovalParallelToken),
            typeof(DynamicTable),
            typeof(DynamicField),
            typeof(DynamicIndex),
            typeof(MigrationRecord),
            typeof(LowCodeApp),
            typeof(LowCodePage),
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
            typeof(TenantDataSource));

        // 创建审批模块数据库索引
        var indexInitializer = scope.ServiceProvider.GetRequiredService<ApprovalIndexInitializer>();
        await indexInitializer.CreateIndexesAsync(cancellationToken);
        var idempotencyIndexInitializer = scope.ServiceProvider.GetRequiredService<IdempotencyIndexInitializer>();
        await idempotencyIndexInitializer.CreateIndexesAsync(cancellationToken);
        var tableViewIndexInitializer = scope.ServiceProvider.GetRequiredService<TableViewIndexInitializer>();
        await tableViewIndexInitializer.CreateIndexesAsync(cancellationToken);

        // 初始化审批模块种子数据（使用 BootstrapAdmin 的 TenantId）
        if (Guid.TryParse(_bootstrapOptions.TenantId, out var seedTenantGuid))
        {
            var approvalSeedService = scope.ServiceProvider.GetRequiredService<ApprovalSeedDataService>();
            var seedTenantId = new TenantId(seedTenantGuid);
            await approvalSeedService.InitializeSeedDataAsync(seedTenantId, cancellationToken);
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
            await EnsureBuiltInSystemConfigsAsync(
                scope.ServiceProvider.GetRequiredService<SystemConfigRepository>(),
                scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>(),
                configTenantId,
                cancellationToken);
        }

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
            (PermissionCodes.ApprovalFlowView, "Approval Flow View", "Api"),
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

            ("安全中心", "/security", null, 10, "M", "Layout", "safety-certificate", null, null, false, false, "0", "0", null, false),
            ("资产管理", "/assets", "/security", 11, "C", "AssetsPage", "database", PermissionCodes.AssetsView, null, false, true, "0", "0", PermissionCodes.AssetsView, false),
            ("审计日志", "/audit", "/security", 12, "C", "AuditPage", "file-search", PermissionCodes.AuditView, null, false, true, "0", "0", PermissionCodes.AuditView, false),
            ("告警管理", "/alert", "/security", 13, "C", "AlertPage", "bell", PermissionCodes.AlertView, null, false, true, "0", "0", PermissionCodes.AlertView, false),

            ("流程中心", "/process", null, 20, "M", "Layout", "cluster", null, null, false, false, "0", "0", null, false),
            ("审批流", "/approval/flows", "/process", 21, "C", "ApprovalFlowsPage", "apartment", PermissionCodes.ApprovalFlowView, null, false, true, "0", "0", PermissionCodes.ApprovalFlowView, false),
            ("工作流设计", "/workflow/designer", "/process", 22, "C", "WorkflowDesignerPage", "branches", workflowPermission.Code, null, false, true, "0", "0", workflowPermission.Code, false),

            ("系统管理", "/system", null, 30, "M", "Layout", "setting", null, null, false, false, "0", "0", null, false),
            ("用户管理", "/settings/org/users", "/system", 31, "C", "system/UsersPage", "user", PermissionCodes.UsersView, null, false, true, "0", "0", PermissionCodes.UsersView, false),
            ("角色管理", "/settings/auth/roles", "/system", 32, "C", "system/RolesPage", "team", PermissionCodes.RolesView, null, false, true, "0", "0", PermissionCodes.RolesView, false),
            ("菜单管理", "/settings/auth/menus", "/system", 33, "C", "system/MenusPage", "menu", PermissionCodes.MenusView, null, false, true, "0", "0", PermissionCodes.MenusView, false),
            ("项目管理", "/settings/projects", "/system", 34, "C", "system/ProjectsPage", "project", PermissionCodes.ProjectsView, null, false, true, "0", "0", PermissionCodes.ProjectsView, false),

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
            ("菜单修改", "/settings/auth/menus:update", "/settings/auth/menus", 3, "F", null, null, PermissionCodes.MenusUpdate, null, false, false, "0", "0", PermissionCodes.MenusUpdate, true)
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
            "/process",
            "/approval/flows",
            "/workflow/designer",
            "/system",
            "/settings/org/users",
            "/settings/auth/roles",
            "/settings/auth/menus",
            "/settings/projects"
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

        var schema = await db.Ado.GetDataTableAsync("PRAGMA table_info('AuthSession');");
        if (!IsColumnNotNull(schema, "RevokedAt"))
        {
            return;
        }

        var result = await db.Ado.UseTranAsync(async () =>
        {
            await db.Ado.ExecuteCommandAsync("ALTER TABLE AuthSession RENAME TO AuthSession_old;", cancellationToken);
            await db.Ado.ExecuteCommandAsync(
                """
                CREATE TABLE AuthSession (
                    Id INTEGER NOT NULL,
                    TenantIdValue TEXT NOT NULL,
                    UserId INTEGER NOT NULL,
                    ClientType TEXT NOT NULL,
                    ClientPlatform TEXT NOT NULL,
                    ClientChannel TEXT NOT NULL,
                    ClientAgent TEXT NOT NULL,
                    IpAddress TEXT NOT NULL,
                    UserAgent TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    LastSeenAt TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    RevokedAt TEXT NULL
                );
                """,
                cancellationToken);
            await db.Ado.ExecuteCommandAsync(
                """
                INSERT INTO AuthSession (
                    Id, TenantIdValue, UserId, ClientType, ClientPlatform, ClientChannel, ClientAgent,
                    IpAddress, UserAgent, CreatedAt, LastSeenAt, ExpiresAt, RevokedAt
                )
                SELECT
                    Id, TenantIdValue, UserId, ClientType, ClientPlatform, ClientChannel, ClientAgent,
                    IpAddress, UserAgent, CreatedAt, LastSeenAt, ExpiresAt, RevokedAt
                FROM AuthSession_old;
                """,
                cancellationToken);
            await db.Ado.ExecuteCommandAsync("DROP TABLE AuthSession_old;", cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("修复 AuthSession 表结构失败。");
        }
    }

    private static async Task EnsureRefreshTokenSchemaAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("RefreshToken", false))
        {
            return;
        }

        var schema = await db.Ado.GetDataTableAsync("PRAGMA table_info('RefreshToken');");
        if (!IsColumnNotNull(schema, "RevokedAt") && !IsColumnNotNull(schema, "ReplacedById"))
        {
            return;
        }

        var result = await db.Ado.UseTranAsync(async () =>
        {
            await db.Ado.ExecuteCommandAsync("ALTER TABLE RefreshToken RENAME TO RefreshToken_old;", cancellationToken);
            await db.Ado.ExecuteCommandAsync(
                """
                CREATE TABLE RefreshToken (
                    Id INTEGER NOT NULL,
                    TenantIdValue TEXT NOT NULL,
                    UserId INTEGER NOT NULL,
                    SessionId INTEGER NOT NULL,
                    TokenHash TEXT NOT NULL,
                    IssuedAt TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    RevokedAt TEXT NULL,
                    ReplacedById INTEGER NULL
                );
                """,
                cancellationToken);
            await db.Ado.ExecuteCommandAsync(
                """
                INSERT INTO RefreshToken (
                    Id, TenantIdValue, UserId, SessionId, TokenHash, IssuedAt, ExpiresAt, RevokedAt, ReplacedById
                )
                SELECT
                    Id, TenantIdValue, UserId, SessionId, TokenHash, IssuedAt, ExpiresAt, RevokedAt, ReplacedById
                FROM RefreshToken_old;
                """,
                cancellationToken);
            await db.Ado.ExecuteCommandAsync("DROP TABLE RefreshToken_old;", cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("修复 RefreshToken 表结构失败。");
        }
    }

    private static bool IsColumnNotNull(DataTable schema, string columnName)
    {
        foreach (DataRow row in schema.Rows)
        {
            var name = row["name"]?.ToString();
            if (!string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var notNull = Convert.ToInt32(row["notnull"]);
            return notNull == 1;
        }

        return false;
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




