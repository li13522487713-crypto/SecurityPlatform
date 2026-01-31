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
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Workflow.Entities;
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
            // Workflow entities
            typeof(PersistedWorkflow),
            typeof(PersistedExecutionPointer),
            typeof(PersistedEvent),
            typeof(PersistedSubscription));

        // 创建审批模块数据库索引
        var indexInitializer = scope.ServiceProvider.GetRequiredService<ApprovalIndexInitializer>();
        await indexInitializer.CreateIndexesAsync(cancellationToken);

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

        var roleIds = new List<long>();
        foreach (var roleCode in roleCodesArray)
        {
            var existingRole = await roleRepository.FindByCodeAsync(tenantId, roleCode, cancellationToken);
            if (existingRole is null)
            {
                var displayName = roleSeedMap.TryGetValue(roleCode, out var seed) ? seed.Name : roleCode;
                var description = roleSeedMap.TryGetValue(roleCode, out seed) ? seed.Description : roleCode;
                var role = new Role(tenantId, displayName, roleCode, idGeneratorAccessor.NextId());
                role.Update(displayName, description);
                role.MarkSystemRole();
                await roleRepository.AddAsync(role, cancellationToken);
                roleIds.Add(role.Id);
            }
            else
            {
                roleIds.Add(existingRole.Id);
            }
        }

        foreach (var seed in roleSeedDefinitions)
        {
            if (roleCodesArray.Contains(seed.Code, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var existingRole = await roleRepository.FindByCodeAsync(tenantId, seed.Code, cancellationToken);
            if (existingRole is not null)
            {
                continue;
            }

            var role = new Role(tenantId, seed.Name, seed.Code, idGeneratorAccessor.NextId());
            role.Update(seed.Name, seed.Description);
            if (seed.IsSystem)
            {
                role.MarkSystemRole();
            }

            await roleRepository.AddAsync(role, cancellationToken);
        }

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
            (PermissionCodes.AssetsCreate, "Assets Create", "Api"),
            (PermissionCodes.ApprovalFlowCreate, "Approval Flow Create", "Api"),
            (PermissionCodes.ApprovalFlowUpdate, "Approval Flow Update", "Api"),
            (PermissionCodes.ApprovalFlowPublish, "Approval Flow Publish", "Api"),
            (PermissionCodes.ApprovalFlowDelete, "Approval Flow Delete", "Api"),
            (PermissionCodes.ApprovalFlowDisable, "Approval Flow Disable", "Api"),
            (PermissionCodes.VisualizationProcessSave, "Visualization Process Save", "Api"),
            (PermissionCodes.VisualizationProcessUpdate, "Visualization Process Update", "Api"),
            (PermissionCodes.VisualizationProcessPublish, "Visualization Process Publish", "Api")
        };

        var permissionIdMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        Permission? adminPermission = null;
        Permission? workflowPermission = null;

        foreach (var seed in permissionSeeds)
        {
            var existingPermission = await db.Queryable<Permission>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.Code == seed.Code)
                .FirstAsync(cancellationToken);
            if (existingPermission is null)
            {
                existingPermission = new Permission(tenantId, seed.Name, seed.Code, seed.Type, idGeneratorAccessor.NextId());
                await db.Insertable(existingPermission).ExecuteCommandAsync(cancellationToken);
            }

            permissionIdMap[existingPermission.Code] = existingPermission.Id;
            if (string.Equals(existingPermission.Code, PermissionCodes.SystemAdmin, StringComparison.OrdinalIgnoreCase))
            {
                adminPermission = existingPermission;
            }

            if (string.Equals(existingPermission.Code, PermissionCodes.WorkflowDesign, StringComparison.OrdinalIgnoreCase))
            {
                workflowPermission = existingPermission;
            }
        }

        if (adminPermission is null || workflowPermission is null)
        {
            throw new InvalidOperationException("初始化权限失败：缺少 system:admin 或 workflow:design。");
        }

        var adminMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/system")
            .FirstAsync(cancellationToken);
        if (adminMenu is null)
        {
            adminMenu = new Menu(
                tenantId,
                "System",
                "/system",
                idGeneratorAccessor.NextId(),
                0,
                0,
                "Layout",
                "settings",
                adminPermission.Code,
                false);
            await db.Insertable(adminMenu).ExecuteCommandAsync(cancellationToken);
        }

        var projectsMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/system/projects")
            .FirstAsync(cancellationToken);
        if (projectsMenu is null)
        {
            projectsMenu = new Menu(
                tenantId,
                "项目管理",
                "/system/projects",
                idGeneratorAccessor.NextId(),
                adminMenu.Id,
                70,
                "system/projects",
                "project",
                PermissionCodes.ProjectsView,
                false);
            await db.Insertable(projectsMenu).ExecuteCommandAsync(cancellationToken);
        }

        var workflowRootMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/workflow")
            .FirstAsync(cancellationToken);
        if (workflowRootMenu is null)
        {
            workflowRootMenu = new Menu(
                tenantId,
                "Workflow",
                "/workflow",
                idGeneratorAccessor.NextId(),
                0,
                10,
                "Layout",
                "workflow",
                adminPermission.Code,
                false);
            await db.Insertable(workflowRootMenu).ExecuteCommandAsync(cancellationToken);
        }

        var workflowDesignerMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/workflow/designer")
            .FirstAsync(cancellationToken);
        if (workflowDesignerMenu is null)
        {
            workflowDesignerMenu = new Menu(
                tenantId,
                "Workflow Designer",
                "/workflow/designer",
                idGeneratorAccessor.NextId(),
                workflowRootMenu.Id,
                20,
                "workflow/designer",
                "workflow",
                workflowPermission.Code,
                false);
            await db.Insertable(workflowDesignerMenu).ExecuteCommandAsync(cancellationToken);
        }

        var homeMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/")
            .FirstAsync(cancellationToken);
        if (homeMenu is null)
        {
            homeMenu = new Menu(
                tenantId,
                "总览",
                "/",
                idGeneratorAccessor.NextId(),
                0,
                0,
                null,
                null,
                adminPermission.Code,
                false);
            await db.Insertable(homeMenu).ExecuteCommandAsync(cancellationToken);
        }

        var assetsMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/assets")
            .FirstAsync(cancellationToken);
        if (assetsMenu is null)
        {
            assetsMenu = new Menu(
                tenantId,
                "资产",
                "/assets",
                idGeneratorAccessor.NextId(),
                0,
                10,
                null,
                null,
                adminPermission.Code,
                false);
            await db.Insertable(assetsMenu).ExecuteCommandAsync(cancellationToken);
        }

        var auditMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/audit")
            .FirstAsync(cancellationToken);
        if (auditMenu is null)
        {
            auditMenu = new Menu(
                tenantId,
                "审计",
                "/audit",
                idGeneratorAccessor.NextId(),
                0,
                20,
                null,
                null,
                adminPermission.Code,
                false);
            await db.Insertable(auditMenu).ExecuteCommandAsync(cancellationToken);
        }

        var alertMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/alert")
            .FirstAsync(cancellationToken);
        if (alertMenu is null)
        {
            alertMenu = new Menu(
                tenantId,
                "告警",
                "/alert",
                idGeneratorAccessor.NextId(),
                0,
                30,
                null,
                null,
                adminPermission.Code,
                false);
            await db.Insertable(alertMenu).ExecuteCommandAsync(cancellationToken);
        }

        var approvalMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/approval/flows")
            .FirstAsync(cancellationToken);
        if (approvalMenu is null)
        {
            approvalMenu = new Menu(
                tenantId,
                "审批流",
                "/approval/flows",
                idGeneratorAccessor.NextId(),
                0,
                40,
                null,
                null,
                adminPermission.Code,
                false);
            await db.Insertable(approvalMenu).ExecuteCommandAsync(cancellationToken);
        }

        var visualizationRootMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/visualization")
            .FirstAsync(cancellationToken);
        if (visualizationRootMenu is null)
        {
            visualizationRootMenu = new Menu(
                tenantId,
                "可视化中心",
                "/visualization",
                idGeneratorAccessor.NextId(),
                0,
                50,
                "Layout",
                "dashboard",
                adminPermission.Code,
                false);
            await db.Insertable(visualizationRootMenu).ExecuteCommandAsync(cancellationToken);
        }

        var visualizationCenterMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/visualization/center")
            .FirstAsync(cancellationToken);
        if (visualizationCenterMenu is null)
        {
            visualizationCenterMenu = new Menu(
                tenantId,
                "总览",
                "/visualization/center",
                idGeneratorAccessor.NextId(),
                visualizationRootMenu.Id,
                10,
                "visualization/center",
                "dashboard",
                adminPermission.Code,
                false);
            await db.Insertable(visualizationCenterMenu).ExecuteCommandAsync(cancellationToken);
        }

        var visualizationDesignerMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/visualization/designer")
            .FirstAsync(cancellationToken);
        if (visualizationDesignerMenu is null)
        {
            visualizationDesignerMenu = new Menu(
                tenantId,
                "设计器",
                "/visualization/designer",
                idGeneratorAccessor.NextId(),
                visualizationRootMenu.Id,
                20,
                "visualization/designer",
                "dashboard",
                adminPermission.Code,
                false);
            await db.Insertable(visualizationDesignerMenu).ExecuteCommandAsync(cancellationToken);
        }

        var visualizationRuntimeMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/visualization/runtime")
            .FirstAsync(cancellationToken);
        if (visualizationRuntimeMenu is null)
        {
            visualizationRuntimeMenu = new Menu(
                tenantId,
                "运行态",
                "/visualization/runtime",
                idGeneratorAccessor.NextId(),
                visualizationRootMenu.Id,
                30,
                "visualization/runtime",
                "dashboard",
                adminPermission.Code,
                false);
            await db.Insertable(visualizationRuntimeMenu).ExecuteCommandAsync(cancellationToken);
        }

        var visualizationGovernanceMenu = await db.Queryable<Menu>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Path == "/visualization/governance")
            .FirstAsync(cancellationToken);
        if (visualizationGovernanceMenu is null)
        {
            visualizationGovernanceMenu = new Menu(
                tenantId,
                "治理中心",
                "/visualization/governance",
                idGeneratorAccessor.NextId(),
                visualizationRootMenu.Id,
                40,
                "visualization/governance",
                "dashboard",
                adminPermission.Code,
                false);
            await db.Insertable(visualizationGovernanceMenu).ExecuteCommandAsync(cancellationToken);
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
            await db.Insertable(department).ExecuteCommandAsync(cancellationToken);
            departmentIdMap[seed.Name] = department.Id;
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

            await db.Insertable(position).ExecuteCommandAsync(cancellationToken);
            positionCodeMap[seed.Code] = position.Id;
        }

        foreach (var roleId in roleIds)
        {
            async Task EnsureRoleMenuAsync(long menuId)
            {
                var exists = await db.Queryable<RoleMenu>()
                    .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.MenuId == menuId)
                    .AnyAsync();
                if (!exists)
                {
                    await roleMenuRepository.AddRangeAsync(
                        new[] { new RoleMenu(tenantId, roleId, menuId, idGeneratorAccessor.NextId()) },
                        cancellationToken);
                }
            }

            foreach (var permissionId in permissionIdMap.Values.Distinct())
            {
                var existsPermission = await db.Queryable<RolePermission>()
                    .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.PermissionId == permissionId)
                    .AnyAsync();
                if (!existsPermission)
                {
                    await rolePermissionRepository.AddRangeAsync(
                        new[] { new RolePermission(tenantId, roleId, permissionId, idGeneratorAccessor.NextId()) },
                        cancellationToken);
                }
            }

            var existsMenu = await db.Queryable<RoleMenu>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.MenuId == adminMenu.Id)
                .AnyAsync();
            if (!existsMenu)
            {
                await roleMenuRepository.AddRangeAsync(
                    new[] { new RoleMenu(tenantId, roleId, adminMenu.Id, idGeneratorAccessor.NextId()) },
                    cancellationToken);
            }

            var existsWorkflowRootMenu = await db.Queryable<RoleMenu>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.MenuId == workflowRootMenu.Id)
                .AnyAsync();
            if (!existsWorkflowRootMenu)
            {
                await roleMenuRepository.AddRangeAsync(
                    new[] { new RoleMenu(tenantId, roleId, workflowRootMenu.Id, idGeneratorAccessor.NextId()) },
                    cancellationToken);
            }

            var existsWorkflowDesignerMenu = await db.Queryable<RoleMenu>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.MenuId == workflowDesignerMenu.Id)
                .AnyAsync();
            if (!existsWorkflowDesignerMenu)
            {
                await roleMenuRepository.AddRangeAsync(
                    new[] { new RoleMenu(tenantId, roleId, workflowDesignerMenu.Id, idGeneratorAccessor.NextId()) },
                    cancellationToken);
            }

            await EnsureRoleMenuAsync(homeMenu.Id);
            await EnsureRoleMenuAsync(assetsMenu.Id);
            await EnsureRoleMenuAsync(auditMenu.Id);
            await EnsureRoleMenuAsync(alertMenu.Id);
            await EnsureRoleMenuAsync(approvalMenu.Id);
            await EnsureRoleMenuAsync(visualizationRootMenu.Id);
            await EnsureRoleMenuAsync(visualizationCenterMenu.Id);
            await EnsureRoleMenuAsync(visualizationDesignerMenu.Id);
            await EnsureRoleMenuAsync(visualizationRuntimeMenu.Id);
            await EnsureRoleMenuAsync(visualizationGovernanceMenu.Id);
            await EnsureRoleMenuAsync(projectsMenu.Id);
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
}




