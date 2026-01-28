using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Alert.Entities;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Assets.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
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
            typeof(ApprovalNodeExecution));

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

        var adminPermission = await db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == "system:admin")
            .FirstAsync(cancellationToken);
        if (adminPermission is null)
        {
            adminPermission = new Permission(tenantId, "System Admin", "system:admin", "Api", idGenerator.NextId());
            await db.Insertable(adminPermission).ExecuteCommandAsync(cancellationToken);
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
                null,
                0,
                "Layout",
                "settings",
                adminPermission.Code,
                false);
            await db.Insertable(adminMenu).ExecuteCommandAsync(cancellationToken);
        }

        foreach (var roleId in roleIds)
        {
            var existsPermission = await db.Queryable<RolePermission>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == roleId && x.PermissionId == adminPermission.Id)
                .AnyAsync();
            if (!existsPermission)
            {
                await rolePermissionRepository.AddRangeAsync(
                    new[] { new RolePermission(tenantId, roleId, adminPermission.Id, idGenerator.NextId()) },
                    cancellationToken);
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
