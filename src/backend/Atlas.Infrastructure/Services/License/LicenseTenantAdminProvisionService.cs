using System.Security.Cryptography;
using System.Text;
using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.License;

/// <summary>
/// 在证书租户下确保 BootstrapAdmin 可用（账号存在、启用且具备基础管理员角色）。
/// </summary>
public sealed class LicenseTenantAdminProvisionService
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly BootstrapAdminOptions _bootstrapOptions;
    private readonly ILogger<LicenseTenantAdminProvisionService> _logger;

    public LicenseTenantAdminProvisionService(
        IUserAccountRepository userAccountRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAppContextAccessor appContextAccessor,
        IOptions<BootstrapAdminOptions> bootstrapOptions,
        ILogger<LicenseTenantAdminProvisionService> logger)
    {
        _userAccountRepository = userAccountRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _idGeneratorAccessor = idGeneratorAccessor;
        _appContextAccessor = appContextAccessor;
        _bootstrapOptions = bootstrapOptions.Value;
        _logger = logger;
    }

    public async Task EnsureBootstrapAdminAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId.IsEmpty)
        {
            throw new BusinessException("证书租户为空，无法绑定管理员账号。", ErrorCodes.ValidationError);
        }

        if (string.IsNullOrWhiteSpace(_bootstrapOptions.Username))
        {
            throw new BusinessException("未配置 BootstrapAdmin.Username，无法绑定管理员账号。", ErrorCodes.ValidationError);
        }

        if (string.IsNullOrWhiteSpace(_bootstrapOptions.Password))
        {
            throw new BusinessException("未配置 BootstrapAdmin.Password，无法绑定管理员账号。", ErrorCodes.ValidationError);
        }

        var roleCodes = ResolveRoleCodes();
        using var contextScope = _appContextAccessor.BeginScope(CreateSystemContext(tenantId));

        var roles = await EnsureRolesAsync(tenantId, roleCodes, cancellationToken);
        var roleIdSet = roles.Select(x => x.Id).ToHashSet();

        var username = _bootstrapOptions.Username.Trim();
        var account = await _userAccountRepository.FindByUsernameAsync(tenantId, username, cancellationToken);
        if (account is null)
        {
            var hashed = _passwordHasher.HashPassword(_bootstrapOptions.Password);
            account = new UserAccount(
                tenantId,
                username,
                username,
                hashed,
                NextIdWithFallback("bootstrap-admin-account"));
            account.UpdateRoles(string.Join(',', roleCodes));
            account.MarkSystemAccount();
            await _userAccountRepository.AddAsync(account, cancellationToken);
        }
        else
        {
            if (!account.IsSystem)
            {
                throw new BusinessException(
                    $"租户 {tenantId.Value} 中已存在同名账号 {username}，且非系统账号，拒绝自动提升权限。",
                    ErrorCodes.ConflictError);
            }

            var changed = false;
            if (!account.IsActive)
            {
                account.Activate();
                changed = true;
            }

            var targetRoles = string.Join(',', roleCodes);
            if (!string.Equals(account.Roles, targetRoles, StringComparison.OrdinalIgnoreCase))
            {
                account.UpdateRoles(targetRoles);
                changed = true;
            }

            if (!account.IsSystem)
            {
                account.MarkSystemAccount();
                changed = true;
            }

            if (changed)
            {
                await _userAccountRepository.UpdateAsync(account, cancellationToken);
            }
        }

        var userRoles = await _userRoleRepository.QueryByUserIdAsync(tenantId, account.Id, cancellationToken);
        var existingRoleIds = userRoles.Select(x => x.RoleId).ToHashSet();
        var missingRoleIds = roleIdSet.Where(id => !existingRoleIds.Contains(id)).ToArray();
        if (missingRoleIds.Length > 0)
        {
            var toInsert = missingRoleIds
                .Select(roleId => new UserRole(tenantId, account.Id, roleId, NextIdWithFallback("bootstrap-admin-user-role")))
                .ToArray();
            await _userRoleRepository.AddRangeAsync(toInsert, cancellationToken);
        }

        _logger.LogInformation("证书租户管理员绑定完成：TenantId={TenantId}, Username={Username}", tenantId.Value, username);
    }

    private async Task<IReadOnlyList<Role>> EnsureRolesAsync(
        TenantId tenantId,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.QueryByCodesAsync(tenantId, roleCodes, cancellationToken);
        var roleMap = existing.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
        var toInsert = new List<Role>();

        foreach (var roleCode in roleCodes)
        {
            if (roleMap.ContainsKey(roleCode))
            {
                continue;
            }

            var role = new Role(
                tenantId,
                ResolveRoleDisplayName(roleCode),
                roleCode,
                NextIdWithFallback($"bootstrap-role-{roleCode}"));
            role.MarkSystemRole();
            role.Update(ResolveRoleDisplayName(roleCode), $"证书激活自动补齐角色：{roleCode}");
            toInsert.Add(role);
            roleMap[roleCode] = role;
        }

        if (toInsert.Count > 0)
        {
            await _roleRepository.AddRangeAsync(toInsert, cancellationToken);
        }

        return roleCodes.Select(code => roleMap[code]).ToArray();
    }

    private static string ResolveRoleDisplayName(string roleCode)
    {
        return roleCode.ToLowerInvariant() switch
        {
            "superadmin" => "超级管理员",
            "admin" => "系统管理员",
            "securityadmin" => "安全管理员",
            "auditadmin" => "审计管理员",
            "assetadmin" => "资产管理员",
            "approvaladmin" => "流程管理员",
            _ => roleCode
        };
    }

    private IReadOnlyList<string> ResolveRoleCodes()
    {
        var roleCodes = _bootstrapOptions.Roles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (roleCodes.Count == 0)
        {
            roleCodes.Add("Admin");
        }

        return roleCodes;
    }

    private IAppContext CreateSystemContext(TenantId tenantId)
    {
        var appId = _appContextAccessor.GetAppId();
        var clientContext = new ClientContext(
            ClientType.Backend,
            ClientPlatform.Web,
            ClientChannel.App,
            ClientAgent.Other);
        return new AppContextSnapshot(tenantId, appId, null, clientContext, null);
    }

    private long NextIdWithFallback(string purpose)
    {
        try
        {
            return _idGeneratorAccessor.NextId();
        }
        catch (BusinessException ex) when (ex.Code == ErrorCodes.ValidationError)
        {
            var seed = $"{purpose}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid():N}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            var fallbackId = BitConverter.ToInt64(hash, 0) & long.MaxValue;
            return fallbackId == 0 ? 1 : fallbackId;
        }
    }
}
