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

namespace Atlas.Infrastructure.Services;

public sealed class UserCommandService : IUserCommandService
{
    private const int PasswordHistoryRetention = 3;

    private readonly IUserAccountRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IUserPositionRepository _userPositionRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IPasswordHistoryRepository _passwordHistoryRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PasswordPolicyOptions _passwordPolicy;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly Atlas.Core.Identity.IProjectContextAccessor _projectContextAccessor;

    public UserCommandService(
        IUserAccountRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IUserPositionRepository userPositionRepository,
        IRoleRepository roleRepository,
        IDepartmentRepository departmentRepository,
        IPositionRepository positionRepository,
        IPasswordHistoryRepository passwordHistoryRepository,
        IPasswordHasher passwordHasher,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        IOptions<PasswordPolicyOptions> passwordPolicy,
        IProjectUserRepository projectUserRepository,
        Atlas.Core.Identity.IProjectContextAccessor projectContextAccessor)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _userPositionRepository = userPositionRepository;
        _roleRepository = roleRepository;
        _departmentRepository = departmentRepository;
        _positionRepository = positionRepository;
        _passwordHistoryRepository = passwordHistoryRepository;
        _passwordHasher = passwordHasher;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _passwordPolicy = passwordPolicy.Value;
        _projectUserRepository = projectUserRepository;
        _projectContextAccessor = projectContextAccessor;
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
        var positionIds = request.PositionIds?.Distinct().ToArray() ?? Array.Empty<long>();
        await EnsurePositionsExistAsync(tenantId, positionIds, cancellationToken);
        user.UpdateRoles(string.Join(',', roles.Select(x => x.Code)));

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRoleRepository.AddRangeAsync(
                roles.Select(role => new UserRole(tenantId, user.Id, role.Id, _idGeneratorAccessor.NextId())).ToArray(),
                cancellationToken);
            await _userDepartmentRepository.AddRangeAsync(
                departmentIds
                    .Select(depId => new UserDepartment(tenantId, user.Id, depId, _idGeneratorAccessor.NextId(), false))
                    .ToArray(),
                cancellationToken);
            await _userPositionRepository.AddRangeAsync(
                positionIds
                    .Select(posId => new UserPosition(tenantId, user.Id, posId, _idGeneratorAccessor.NextId(), false))
                    .ToArray(),
                cancellationToken);

            var projectContext = _projectContextAccessor.GetCurrent();
            if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
            {
                await _projectUserRepository.AddRangeAsync(
                    new[] { new ProjectUser(tenantId, projectContext.ProjectId.Value, user.Id, _idGeneratorAccessor.NextId()) },
                    cancellationToken);
            }
        }, cancellationToken);

        return user.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureUserInProjectAsync(tenantId, userId, cancellationToken);
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
        await EnsureUserInProjectAsync(tenantId, userId, cancellationToken);
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        var roles = await EnsureRolesExistAsync(tenantId, roleIds.Distinct().ToArray(), cancellationToken);
        user.UpdateRoles(string.Join(',', roles.Select(x => x.Code)));

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userRoleRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userRoleRepository.AddRangeAsync(
                roles.Select(role => new UserRole(tenantId, userId, role.Id, _idGeneratorAccessor.NextId())).ToArray(),
                cancellationToken);
            await _userRepository.UpdateAsync(user, cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateDepartmentsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken)
    {
        await EnsureUserInProjectAsync(tenantId, userId, cancellationToken);
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        await EnsureDepartmentsExistAsync(tenantId, departmentIds, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userDepartmentRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userDepartmentRepository.AddRangeAsync(
                departmentIds.Distinct()
                    .Select(depId => new UserDepartment(tenantId, userId, depId, _idGeneratorAccessor.NextId(), false))
                    .ToArray(),
                cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdatePositionsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> positionIds,
        CancellationToken cancellationToken)
    {
        await EnsureUserInProjectAsync(tenantId, userId, cancellationToken);
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        await EnsurePositionsExistAsync(tenantId, positionIds, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userPositionRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userPositionRepository.AddRangeAsync(
                positionIds.Distinct()
                    .Select(posId => new UserPosition(tenantId, userId, posId, _idGeneratorAccessor.NextId(), false))
                    .ToArray(),
                cancellationToken);
        }, cancellationToken);
    }

    public async Task ChangePasswordAsync(
        TenantId tenantId,
        long userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        if (!_passwordHasher.VerifyHashedPassword(user.PasswordHash, currentPassword))
        {
            throw new BusinessException("Current password is incorrect.", ErrorCodes.Unauthorized);
        }

        if (_passwordHasher.VerifyHashedPassword(user.PasswordHash, newPassword))
        {
            throw new BusinessException("New password must be different from current password.", ErrorCodes.ValidationError);
        }

        var recentHistories = await _passwordHistoryRepository.GetRecentAsync(
            tenantId,
            userId,
            PasswordHistoryRetention,
            cancellationToken);
        foreach (var history in recentHistories)
        {
            if (_passwordHasher.VerifyHashedPassword(history.PasswordHash, newPassword))
            {
                throw new BusinessException("New password cannot reuse recently used passwords.", ErrorCodes.ValidationError);
            }
        }

        if (!PasswordPolicy.IsCompliant(newPassword, _passwordPolicy, out var message))
        {
            throw new BusinessException(message, ErrorCodes.ValidationError);
        }

        var oldPasswordHash = user.PasswordHash;
        var now = DateTimeOffset.UtcNow;
        var passwordHash = _passwordHasher.HashPassword(newPassword);
        user.UpdatePassword(passwordHash, now);

        var historyEntry = new PasswordHistory(
            tenantId,
            userId,
            oldPasswordHash,
            _idGeneratorAccessor.NextId(),
            now);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _passwordHistoryRepository.AddAsync(historyEntry, cancellationToken);
            await _passwordHistoryRepository.DeleteExceptRecentAsync(
                tenantId,
                userId,
                PasswordHistoryRetention,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateProfileAsync(
        TenantId tenantId,
        long userId,
        string displayName,
        string? email,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        user.UpdateProfile(displayName, email, phoneNumber);
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureUserInProjectAsync(tenantId, userId, cancellationToken);
        var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            throw new BusinessException("User not found.", ErrorCodes.NotFound);
        }

        if (user.IsSystem)
        {
            throw new BusinessException("System user cannot be deleted.", ErrorCodes.Forbidden);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userRoleRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userDepartmentRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userPositionRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _projectUserRepository.DeleteByUserIdAsync(tenantId, userId, cancellationToken);
            await _userRepository.DeleteAsync(tenantId, userId, cancellationToken);
        }, cancellationToken);
    }

    private async Task EnsureUserInProjectAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        var projectContext = _projectContextAccessor.GetCurrent();
        if (!projectContext.IsEnabled || !projectContext.ProjectId.HasValue)
        {
            return;
        }

        var hasMembership = await _projectUserRepository.ExistsAsync(
            tenantId,
            projectContext.ProjectId.Value,
            userId,
            cancellationToken);
        if (!hasMembership)
        {
            throw new BusinessException("User not in current project.", ErrorCodes.Forbidden);
        }
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

    private async Task EnsurePositionsExistAsync(
        TenantId tenantId,
        IReadOnlyList<long> positionIds,
        CancellationToken cancellationToken)
    {
        if (positionIds.Count == 0)
        {
            return;
        }

        var distinctIds = positionIds.Distinct().ToArray();
        var positions = await _positionRepository.QueryByIdsAsync(tenantId, distinctIds, cancellationToken);
        if (positions.Count != distinctIds.Length)
        {
            throw new BusinessException("Position not found.", ErrorCodes.ValidationError);
        }
    }
}




