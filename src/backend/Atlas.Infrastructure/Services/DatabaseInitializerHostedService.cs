using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Application.Identity;
using Atlas.Core.Abstractions;
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
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        db.CodeFirst.InitTables(
            typeof(UserAccount),
            typeof(Role),
            typeof(Permission),
            typeof(UserRole),
            typeof(RolePermission),
            typeof(Department),
            typeof(UserDepartment),
            typeof(Menu),
            typeof(RoleMenu),
            typeof(AuditRecord),
            typeof(Asset),
            typeof(AlertRecord),
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
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        var userRoleRepository = scope.ServiceProvider.GetRequiredService<IUserRoleRepository>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var rolePermissionRepository = scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>();
        var roleMenuRepository = scope.ServiceProvider.GetRequiredService<IRoleMenuRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var roleCodes = _bootstrapOptions.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var roleIds = new List<long>();
        foreach (var roleCode in roleCodes)
        {
            var existingRole = await roleRepository.FindByCodeAsync(tenantId, roleCode, cancellationToken);
            if (existingRole is null)
            {
                var role = new Role(tenantId, roleCode, roleCode, idGenerator.NextId());
                role.Update(roleCode, roleCode);
                role.MarkSystemRole();
                await roleRepository.AddAsync(role, cancellationToken);
                roleIds.Add(role.Id);
            }
            else
            {
                roleIds.Add(existingRole.Id);
            }
        }

        var permissionSeeds = new (string Code, string Name, string Type)[]
        {
            (PermissionCodes.SystemAdmin, "System Admin", "Api"),
            (PermissionCodes.WorkflowDesign, "Workflow Designer", "Menu"),
            (PermissionCodes.UsersView, "Users View", "Api"),
            (PermissionCodes.UsersCreate, "Users Create", "Api"),
            (PermissionCodes.UsersUpdate, "Users Update", "Api"),
            (PermissionCodes.UsersAssignRoles, "Users Assign Roles", "Api"),
            (PermissionCodes.UsersAssignDepartments, "Users Assign Departments", "Api"),
            (PermissionCodes.RolesView, "Roles View", "Api"),
            (PermissionCodes.RolesCreate, "Roles Create", "Api"),
            (PermissionCodes.RolesUpdate, "Roles Update", "Api"),
            (PermissionCodes.RolesAssignPermissions, "Roles Assign Permissions", "Api"),
            (PermissionCodes.RolesAssignMenus, "Roles Assign Menus", "Api"),
            (PermissionCodes.PermissionsView, "Permissions View", "Api"),
            (PermissionCodes.PermissionsCreate, "Permissions Create", "Api"),
            (PermissionCodes.PermissionsUpdate, "Permissions Update", "Api"),
            (PermissionCodes.DepartmentsView, "Departments View", "Api"),
            (PermissionCodes.DepartmentsAll, "Departments All", "Api"),
            (PermissionCodes.DepartmentsCreate, "Departments Create", "Api"),
            (PermissionCodes.DepartmentsUpdate, "Departments Update", "Api"),
            (PermissionCodes.MenusView, "Menus View", "Api"),
            (PermissionCodes.MenusAll, "Menus All", "Api"),
            (PermissionCodes.MenusCreate, "Menus Create", "Api"),
            (PermissionCodes.MenusUpdate, "Menus Update", "Api"),
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
                existingPermission = new Permission(tenantId, seed.Name, seed.Code, seed.Type, idGenerator.NextId());
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
                idGenerator.NextId(),
                0,
                0,
                "Layout",
                "settings",
                adminPermission.Code,
                false);
            await db.Insertable(adminMenu).ExecuteCommandAsync(cancellationToken);
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
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
                idGenerator.NextId(),
                visualizationRootMenu.Id,
                40,
                "visualization/governance",
                "dashboard",
                adminPermission.Code,
                false);
            await db.Insertable(visualizationGovernanceMenu).ExecuteCommandAsync(cancellationToken);
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
                        new[] { new RoleMenu(tenantId, roleId, menuId, idGenerator.NextId()) },
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
                        new[] { new RolePermission(tenantId, roleId, permissionId, idGenerator.NextId()) },
                        cancellationToken);
                }
            }

            var existsMenu = await db.Queryable<RoleMenu>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.MenuId == adminMenu.Id)
                .AnyAsync();
            if (!existsMenu)
            {
                await roleMenuRepository.AddRangeAsync(
                    new[] { new RoleMenu(tenantId, roleId, adminMenu.Id, idGenerator.NextId()) },
                    cancellationToken);
            }

            var existsWorkflowRootMenu = await db.Queryable<RoleMenu>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.MenuId == workflowRootMenu.Id)
                .AnyAsync();
            if (!existsWorkflowRootMenu)
            {
                await roleMenuRepository.AddRangeAsync(
                    new[] { new RoleMenu(tenantId, roleId, workflowRootMenu.Id, idGenerator.NextId()) },
                    cancellationToken);
            }

            var existsWorkflowDesignerMenu = await db.Queryable<RoleMenu>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.MenuId == workflowDesignerMenu.Id)
                .AnyAsync();
            if (!existsWorkflowDesignerMenu)
            {
                await roleMenuRepository.AddRangeAsync(
                    new[] { new RoleMenu(tenantId, roleId, workflowDesignerMenu.Id, idGenerator.NextId()) },
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
        }

        var existing = await userRepository.FindByUsernameAsync(tenantId, _bootstrapOptions.Username, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateRoles(string.Join(',', roleCodes));
            await userRepository.UpdateAsync(existing, cancellationToken);
            await userRoleRepository.DeleteByUserIdAsync(tenantId, existing.Id, cancellationToken);
            await userRoleRepository.AddRangeAsync(
                roleIds.Select(roleId => new UserRole(tenantId, existing.Id, roleId, idGenerator.NextId())).ToArray(),
                cancellationToken);
            return;
        }

        var hashed = passwordHasher.HashPassword(_bootstrapOptions.Password);
        var account = new UserAccount(tenantId, _bootstrapOptions.Username, _bootstrapOptions.Username, hashed, idGenerator.NextId());
        account.UpdateRoles(string.Join(',', roleCodes));
        account.MarkSystemAccount();
        await userRepository.AddAsync(account, cancellationToken);
        await userRoleRepository.AddRangeAsync(
            roleIds.Select(roleId => new UserRole(tenantId, account.Id, roleId, idGenerator.NextId())).ToArray(),
            cancellationToken);
        _logger.LogInformation("已创建BootstrapAdmin账号：{Username}", _bootstrapOptions.Username);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
