using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Security;
using Atlas.Application.Options;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class UserCommandService : IUserCommandService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGenerator _idGenerator;
    private readonly ISqlSugarClient _db;
    private readonly PasswordPolicyOptions _passwordPolicy;

    public UserCommandService(
        IUserAccountRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IRoleRepository roleRepository,
        IDepartmentRepository departmentRepository,
        IPasswordHasher passwordHasher,
        IIdGenerator idGenerator,
        ISqlSugarClient db,
        IOptions<PasswordPolicyOptions> passwordPolicy)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _roleRepository = roleRepository;
        _departmentRepository = departmentRepository;
        _passwordHasher = passwordHasher;
        _idGenerator = idGenerator;
        _db = db;
        _passwordPolicy = passwordPolicy.Value;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        UserCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByUsernameAsync(tenantId, request.Username, cancellationToken))
        {
            throw new BusinessException("Username already exists.", ErrorCodes.ValidationError);
        }

        if (!PasswordPolicy.IsCompliant(request.Password, _passwordPolicy, out _))
        {
            throw new BusinessException("Password policy violation.", ErrorCodes.ValidationError);
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new UserAccount(tenantId, request.Username, request.DisplayName, passwordHash, id);
        user.UpdateProfile(request.DisplayName, request.Email, request.PhoneNumber);

        if (!request.IsActive)
        {
            user.Deactivate();
        }

        var roleIds = request.RoleIds?.Distinct().ToArray() ?? Array.Empty<long>();
        var roles = await EnsureRolesExistAsync(tenantId, roleIds, cancellationToken);
        var departmentIds = request.DepartmentIds?.Distinct().ToArray() ?? Array.Empty<long>();
        await EnsureDepartmentsExistAsync(tenantId, departmentIds, cancellationToken);
        user.UpdateRoles(string.Join(',', roles.Select(x => x.Code)));

        await _db.Ado.UseTranAsync(async () =>
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRoleRepository.AddRangeAsync(
                roles.Select(role => new UserRole(tenantId, user.Id, role.Id, _idGenerator.NextId())).ToArray(),
                cancellationToken);
            await _userDepartmentRepository.AddRangeAsync(
                departmentIds
                    .Select(depId => new UserDepartment(tenantId, user.Id, depId, _idGenerator.NextId(), false))
                    .ToArray(),
                cancellationToken);
        });

        return user.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        user.UpdateProfile(request.DisplayName, request.Email, request.PhoneNumber);
        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task UpdateRolesAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        var roles = await EnsureRolesExistAsync(tenantId, roleIds.Distinct().ToArray(), cancellationToken);
        user.UpdateRoles(string.Join(',', roles.Select(x => x.Code)));

        await _db.Ado.UseTranAsync(async () =>
        {
            await _userRoleRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userRoleRepository.AddRangeAsync(
                roles.Select(role => new UserRole(tenantId, userId, role.Id, _idGenerator.NextId())).ToArray(),
                cancellationToken);
            await _userRepository.UpdateAsync(user, cancellationToken);
        });
    }

    public async Task UpdateDepartmentsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        await EnsureDepartmentsExistAsync(tenantId, departmentIds, cancellationToken);

        await _db.Ado.UseTranAsync(async () =>
        {
            await _userDepartmentRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userDepartmentRepository.AddRangeAsync(
                departmentIds.Distinct()
                    .Select(depId => new UserDepartment(tenantId, userId, depId, _idGenerator.NextId(), false))
                    .ToArray(),
                cancellationToken);
        });
    }

    private async Task<IReadOnlyList<Role>> EnsureRolesExistAsync(
        TenantId tenantId,
        IReadOnlyList<long> roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<Role>();
        }

        var distinctIds = roleIds.Distinct().ToArray();
        var roles = await _roleRepository.QueryByIdsAsync(tenantId, distinctIds, cancellationToken);
        if (roles.Count != distinctIds.Length)
        {
            throw new BusinessException("Role not found.", ErrorCodes.ValidationError);
        }

        return roles;
    }

    private async Task EnsureDepartmentsExistAsync(
        TenantId tenantId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken)
    {
        if (departmentIds.Count == 0)
        {
            return;
        }

        var distinctIds = departmentIds.Distinct().ToArray();
        var departments = await _departmentRepository.QueryByIdsAsync(tenantId, distinctIds, cancellationToken);
        if (departments.Count != distinctIds.Length)
        {
            throw new BusinessException("Department not found.", ErrorCodes.ValidationError);
        }
    }
}
