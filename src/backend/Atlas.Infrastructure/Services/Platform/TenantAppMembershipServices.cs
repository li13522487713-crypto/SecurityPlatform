using Atlas.Application.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class TenantAppMemberQueryService : ITenantAppMemberQueryService
{
    private readonly ILowCodeAppRepository _lowCodeAppRepository;
    private readonly IAppMemberRepository _appMemberRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IAppRoleRepository _appRoleRepository;

    public TenantAppMemberQueryService(
        ILowCodeAppRepository lowCodeAppRepository,
        IAppMemberRepository appMemberRepository,
        IUserAccountRepository userAccountRepository,
        IAppUserRoleRepository appUserRoleRepository,
        IAppRoleRepository appRoleRepository)
    {
        _lowCodeAppRepository = lowCodeAppRepository;
        _appMemberRepository = appMemberRepository;
        _userAccountRepository = userAccountRepository;
        _appUserRoleRepository = appUserRoleRepository;
        _appRoleRepository = appRoleRepository;
    }

    public async Task<PagedResult<TenantAppMemberListItem>> QueryAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedUsers(app);

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var (members, totalCount) = await _appMemberRepository.QueryPageAsync(
            tenantId,
            appId,
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);
        if (members.Count == 0)
        {
            return new PagedResult<TenantAppMemberListItem>(Array.Empty<TenantAppMemberListItem>(), totalCount, pageIndex, pageSize);
        }

        var userIds = members.Select(x => x.UserId).Distinct().ToArray();
        var users = await _userAccountRepository.QueryByIdsAsync(tenantId, userIds, cancellationToken);
        var userMap = users.ToDictionary(x => x.Id);

        var userRoles = await _appUserRoleRepository.QueryByUserIdsAsync(tenantId, appId, userIds, cancellationToken);
        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        var roleMap = roleIds.Length == 0
            ? new Dictionary<long, AppRole>()
            : (await _appRoleRepository.QueryByIdsAsync(tenantId, appId, roleIds, cancellationToken))
                .ToDictionary(x => x.Id);

        var roleIdsByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.RoleId).Distinct().ToArray());

        var items = members
            .Select(member =>
            {
                userMap.TryGetValue(member.UserId, out var user);
                roleIdsByUser.TryGetValue(member.UserId, out var memberRoleIds);
                memberRoleIds ??= Array.Empty<long>();
                var roleNames = memberRoleIds
                    .Select(roleId => roleMap.TryGetValue(roleId, out var role) ? role.Name : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToArray();

                return new TenantAppMemberListItem(
                    member.UserId.ToString(),
                    user?.Username ?? member.UserId.ToString(),
                    user?.DisplayName ?? user?.Username ?? member.UserId.ToString(),
                    user?.IsActive ?? false,
                    member.JoinedAt.ToString("O"),
                    memberRoleIds.Select(x => x.ToString()).ToArray(),
                    roleNames);
            })
            .ToArray();

        return new PagedResult<TenantAppMemberListItem>(items, totalCount, pageIndex, pageSize);
    }

    public async Task<TenantAppMemberDetail?> GetByUserIdAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedUsers(app);

        var member = (await _appMemberRepository.QueryByUserIdsAsync(
                tenantId,
                appId,
                new[] { userId },
                cancellationToken))
            .FirstOrDefault();
        if (member is null)
        {
            return null;
        }

        var user = await _userAccountRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        var userRoles = await _appUserRoleRepository.QueryByUserIdsAsync(tenantId, appId, new[] { userId }, cancellationToken);
        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToArray();
        var roleMap = roleIds.Length == 0
            ? new Dictionary<long, AppRole>()
            : (await _appRoleRepository.QueryByIdsAsync(tenantId, appId, roleIds, cancellationToken))
                .ToDictionary(x => x.Id);
        var roleNames = roleIds
            .Select(roleId => roleMap.TryGetValue(roleId, out var role) ? role.Name : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        return new TenantAppMemberDetail(
            userId.ToString(),
            user?.Username ?? userId.ToString(),
            user?.DisplayName ?? user?.Username ?? userId.ToString(),
            user?.Email,
            user?.PhoneNumber,
            user?.IsActive ?? false,
            member.JoinedAt.ToString("O"),
            roleIds.Select(x => x.ToString()).ToArray(),
            roleNames);
    }

    private async Task<Atlas.Domain.LowCode.Entities.LowCodeApp> RequireAppAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        var app = await _lowCodeAppRepository.GetByIdAsync(tenantId, appId, cancellationToken);
        if (app is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用实例不存在。");
        }

        return app;
    }

    private static void EnsureDedicatedUsers(Atlas.Domain.LowCode.Entities.LowCodeApp app)
    {
        if (app.UseSharedUsers)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前应用启用了共享用户，无法维护应用成员。");
        }
    }
}

public sealed class TenantAppMemberCommandService : ITenantAppMemberCommandService
{
    private readonly ILowCodeAppRepository _lowCodeAppRepository;
    private readonly IAppMemberRepository _appMemberRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TenantAppMemberCommandService(
        ILowCodeAppRepository lowCodeAppRepository,
        IAppMemberRepository appMemberRepository,
        IAppRoleRepository appRoleRepository,
        IUserAccountRepository userAccountRepository,
        IAppUserRoleRepository appUserRoleRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        IAuditWriter auditWriter,
        ICurrentUserAccessor currentUserAccessor)
    {
        _lowCodeAppRepository = lowCodeAppRepository;
        _appMemberRepository = appMemberRepository;
        _appRoleRepository = appRoleRepository;
        _userAccountRepository = userAccountRepository;
        _appUserRoleRepository = appUserRoleRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
        _auditWriter = auditWriter;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task AddMembersAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        TenantAppMemberAssignRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedUsers(app);

        var userIds = request.UserIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (userIds.Length == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "至少选择一名应用成员。");
        }

        var users = await _userAccountRepository.QueryByIdsAsync(tenantId, userIds, cancellationToken);
        if (users.Count != userIds.Length)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "存在无效的成员用户。");
        }

        var roleIds = request.RoleIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (roleIds.Length > 0)
        {
            EnsureDedicatedRoles(app);
            var roles = await _appRoleRepository.QueryByIdsAsync(tenantId, appId, roleIds, cancellationToken);
            if (roles.Count != roleIds.Length)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用角色。");
            }
        }

        var existingMembers = await _appMemberRepository.QueryByUserIdsAsync(tenantId, appId, userIds, cancellationToken);
        var existingUserSet = existingMembers.Select(x => x.UserId).ToHashSet();
        var now = _timeProvider.GetUtcNow();
        var newMembers = userIds
            .Where(userId => !existingUserSet.Contains(userId))
            .Select(userId => new AppMember(
                tenantId,
                appId,
                userId,
                operatorUserId,
                now,
                _idGeneratorAccessor.NextId()))
            .ToArray();
        await _appMemberRepository.AddRangeAsync(newMembers, cancellationToken);

        if (roleIds.Length == 0)
        {
            await WriteAuditAsync(
                tenantId,
                ResolveActor(operatorUserId),
                "Platform.AppMember.Assigned",
                $"appId={appId};userIds={string.Join(',', userIds)};roleIds=",
                cancellationToken);
            return;
        }

        var existingRoleMappings = await _appUserRoleRepository.QueryByUserIdsAsync(tenantId, appId, userIds, cancellationToken);
        var mappingSet = existingRoleMappings
            .Select(x => $"{x.UserId}:{x.RoleId}")
            .ToHashSet(StringComparer.Ordinal);
        var mappingsToAdd = new List<AppUserRole>(userIds.Length * roleIds.Length);
        foreach (var userId in userIds)
        {
            foreach (var roleId in roleIds)
            {
                var key = $"{userId}:{roleId}";
                if (!mappingSet.Contains(key))
                {
                    mappingsToAdd.Add(new AppUserRole(
                        tenantId,
                        appId,
                        userId,
                        roleId,
                        _idGeneratorAccessor.NextId()));
                }
            }
        }

        await _appUserRoleRepository.AddRangeAsync(mappingsToAdd, cancellationToken);

        await WriteAuditAsync(
            tenantId,
            ResolveActor(operatorUserId),
            "Platform.AppMember.Assigned",
            $"appId={appId};userIds={string.Join(',', userIds)};roleIds={string.Join(',', roleIds)}",
            cancellationToken);
    }

    public async Task UpdateMemberRolesAsync(
        TenantId tenantId,
        long appId,
        long userId,
        TenantAppMemberUpdateRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedUsers(app);
        EnsureDedicatedRoles(app);

        var exists = await _appMemberRepository.ExistsAsync(tenantId, appId, userId, cancellationToken);
        if (!exists)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用成员不存在。");
        }

        var roleIds = request.RoleIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (roleIds.Length > 0)
        {
            var roles = await _appRoleRepository.QueryByIdsAsync(tenantId, appId, roleIds, cancellationToken);
            if (roles.Count != roleIds.Length)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用角色。");
            }
        }

        await _appUserRoleRepository.DeleteByUserIdAsync(tenantId, appId, userId, cancellationToken);
        if (roleIds.Length == 0)
        {
            await WriteAuditAsync(
                tenantId,
                ResolveActor(),
                "Platform.AppMember.RolesUpdated",
                $"appId={appId};userId={userId};roleIds=",
                cancellationToken);
            return;
        }

        var mappings = roleIds
            .Select(roleId => new AppUserRole(
                tenantId,
                appId,
                userId,
                roleId,
                _idGeneratorAccessor.NextId()))
            .ToArray();
        await _appUserRoleRepository.AddRangeAsync(mappings, cancellationToken);

        await WriteAuditAsync(
            tenantId,
            ResolveActor(),
            "Platform.AppMember.RolesUpdated",
            $"appId={appId};userId={userId};roleIds={string.Join(',', roleIds)}",
            cancellationToken);
    }

    public async Task RemoveMemberAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedUsers(app);

        await _appUserRoleRepository.DeleteByUserIdAsync(tenantId, appId, userId, cancellationToken);
        await _appMemberRepository.DeleteAsync(tenantId, appId, userId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            ResolveActor(),
            "Platform.AppMember.Removed",
            $"appId={appId};userId={userId}",
            cancellationToken);
    }

    private async Task<Atlas.Domain.LowCode.Entities.LowCodeApp> RequireAppAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        var app = await _lowCodeAppRepository.GetByIdAsync(tenantId, appId, cancellationToken);
        if (app is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用实例不存在。");
        }

        return app;
    }

    private static void EnsureDedicatedUsers(Atlas.Domain.LowCode.Entities.LowCodeApp app)
    {
        if (app.UseSharedUsers)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前应用启用了共享用户，无法维护应用成员。");
        }
    }

    private static void EnsureDedicatedRoles(Atlas.Domain.LowCode.Entities.LowCodeApp app)
    {
        if (app.UseSharedRoles)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前应用启用了共享角色，无法维护应用角色绑定。");
        }
    }

    private string ResolveActor(long? fallbackUserId = null)
    {
        var current = _currentUserAccessor.GetCurrentUser();
        if (current is not null)
        {
            return current.Username;
        }

        return fallbackUserId.HasValue && fallbackUserId.Value > 0
            ? fallbackUserId.Value.ToString()
            : "system";
    }

    private async Task WriteAuditAsync(
        TenantId tenantId,
        string actor,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var record = new AuditRecord(
            tenantId,
            actor,
            action,
            "Success",
            target,
            null,
            null);
        await _auditWriter.WriteAsync(record, cancellationToken);
    }
}

public sealed class TenantAppRoleQueryService : ITenantAppRoleQueryService
{
    private readonly ILowCodeAppRepository _lowCodeAppRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IAppRolePermissionRepository _appRolePermissionRepository;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IAppMemberRepository _appMemberRepository;

    public TenantAppRoleQueryService(
        ILowCodeAppRepository lowCodeAppRepository,
        IAppRoleRepository appRoleRepository,
        IAppRolePermissionRepository appRolePermissionRepository,
        IAppUserRoleRepository appUserRoleRepository,
        IAppMemberRepository appMemberRepository)
    {
        _lowCodeAppRepository = lowCodeAppRepository;
        _appRoleRepository = appRoleRepository;
        _appRolePermissionRepository = appRolePermissionRepository;
        _appUserRoleRepository = appUserRoleRepository;
        _appMemberRepository = appMemberRepository;
    }

    public async Task<PagedResult<TenantAppRoleListItem>> QueryAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedRoles(app);

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var (roles, totalCount) = await _appRoleRepository.QueryPageAsync(
            tenantId,
            appId,
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);
        if (roles.Count == 0)
        {
            return new PagedResult<TenantAppRoleListItem>(Array.Empty<TenantAppRoleListItem>(), totalCount, pageIndex, pageSize);
        }

        var roleIds = roles.Select(x => x.Id).Distinct().ToArray();
        var permissions = await _appRolePermissionRepository.QueryByRoleIdsAsync(tenantId, appId, roleIds, cancellationToken);
        var roleMembers = await _appUserRoleRepository.QueryByRoleIdsAsync(tenantId, appId, roleIds, cancellationToken);
        var permissionMap = permissions
            .GroupBy(x => x.RoleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => x.PermissionCode)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        var memberCountMap = roleMembers
            .GroupBy(x => x.RoleId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.UserId).Distinct().Count());

        var items = roles
            .Select(role =>
            {
                permissionMap.TryGetValue(role.Id, out var permissionCodes);
                permissionCodes ??= Array.Empty<string>();
                memberCountMap.TryGetValue(role.Id, out var memberCount);
                return new TenantAppRoleListItem(
                    role.Id.ToString(),
                    role.Code,
                    role.Name,
                    role.Description,
                    role.IsSystem,
                    memberCount,
                    permissionCodes);
            })
            .ToArray();

        return new PagedResult<TenantAppRoleListItem>(items, totalCount, pageIndex, pageSize);
    }

    public async Task<TenantAppRoleDetail?> GetByIdAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedRoles(app);

        var role = await _appRoleRepository.FindByIdAsync(tenantId, appId, roleId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var permissions = await _appRolePermissionRepository.QueryByRoleIdsAsync(tenantId, appId, new[] { roleId }, cancellationToken);
        var memberMappings = await _appUserRoleRepository.QueryByRoleIdsAsync(tenantId, appId, new[] { roleId }, cancellationToken);
        var permissionCodes = permissions
            .Select(x => x.PermissionCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var memberCount = memberMappings.Select(x => x.UserId).Distinct().Count();

        return new TenantAppRoleDetail(
            role.Id.ToString(),
            role.Code,
            role.Name,
            role.Description,
            role.IsSystem,
            role.CreatedAt.ToString("O"),
            role.UpdatedAt.ToString("O"),
            memberCount,
            permissionCodes);
    }

    public async Task<TenantAppRoleGovernanceOverview> GetGovernanceOverviewAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedRoles(app);

        var roles = await _appRoleRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        if (roles.Count == 0)
        {
            return new TenantAppRoleGovernanceOverview(
                appId.ToString(),
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<TenantAppRoleGovernanceItem>());
        }

        var roleIds = roles.Select(item => item.Id).ToArray();
        var permissionsTask = _appRolePermissionRepository.QueryByRoleIdsAsync(tenantId, appId, roleIds, cancellationToken);
        var userRoleMappingsTask = _appUserRoleRepository.QueryByRoleIdsAsync(tenantId, appId, roleIds, cancellationToken);
        var appMembersTask = _appMemberRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        await Task.WhenAll(permissionsTask, userRoleMappingsTask, appMembersTask);

        var permissions = permissionsTask.Result;
        var userRoleMappings = userRoleMappingsTask.Result;
        var appMembers = appMembersTask.Result;

        var permissionCountByRoleId = permissions
            .GroupBy(item => item.RoleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.PermissionCode)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count());
        var coveredUserCountByRoleId = userRoleMappings
            .GroupBy(item => item.RoleId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.UserId).Distinct().Count());
        var coveredUserIds = userRoleMappings
            .Select(item => item.UserId)
            .Distinct()
            .ToHashSet();

        var governanceItems = roles
            .Select(role =>
            {
                permissionCountByRoleId.TryGetValue(role.Id, out var permissionCount);
                coveredUserCountByRoleId.TryGetValue(role.Id, out var memberCount);
                return new TenantAppRoleGovernanceItem(
                    role.Id.ToString(),
                    role.Code,
                    role.Name,
                    role.IsSystem,
                    memberCount,
                    permissionCount,
                    permissionCount > 0);
            })
            .ToArray();

        var totalMembers = appMembers.Select(item => item.UserId).Distinct().Count();
        var coveredMembers = coveredUserIds.Count;
        var uncoveredMembers = Math.Max(0, totalMembers - coveredMembers);
        var permissionCoverageRate = totalMembers == 0
            ? 0
            : decimal.Round((decimal)coveredMembers / totalMembers, 4, MidpointRounding.AwayFromZero);

        return new TenantAppRoleGovernanceOverview(
            appId.ToString(),
            roles.Count,
            roles.Count(item => item.IsSystem),
            roles.Count(item => !item.IsSystem),
            totalMembers,
            coveredMembers,
            uncoveredMembers,
            permissionCoverageRate,
            governanceItems);
    }

    private async Task<Atlas.Domain.LowCode.Entities.LowCodeApp> RequireAppAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        var app = await _lowCodeAppRepository.GetByIdAsync(tenantId, appId, cancellationToken);
        if (app is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用实例不存在。");
        }

        return app;
    }

    private static void EnsureDedicatedRoles(Atlas.Domain.LowCode.Entities.LowCodeApp app)
    {
        if (app.UseSharedRoles)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前应用启用了共享角色，无法维护应用角色。");
        }
    }
}

public sealed class TenantAppRoleCommandService : ITenantAppRoleCommandService
{
    private readonly ILowCodeAppRepository _lowCodeAppRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IAppRolePermissionRepository _appRolePermissionRepository;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public TenantAppRoleCommandService(
        ILowCodeAppRepository lowCodeAppRepository,
        IAppRoleRepository appRoleRepository,
        IAppRolePermissionRepository appRolePermissionRepository,
        IAppUserRoleRepository appUserRoleRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        IAuditWriter auditWriter,
        ICurrentUserAccessor currentUserAccessor)
    {
        _lowCodeAppRepository = lowCodeAppRepository;
        _appRoleRepository = appRoleRepository;
        _appRolePermissionRepository = appRolePermissionRepository;
        _appUserRoleRepository = appUserRoleRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
        _auditWriter = auditWriter;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long appId,
        long operatorUserId,
        TenantAppRoleCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedRoles(app);

        var normalizedCode = NormalizeCode(request.Code);
        var existed = await _appRoleRepository.FindByCodeAsync(tenantId, appId, normalizedCode, cancellationToken);
        if (existed is not null)
        {
            throw new BusinessException(ErrorCodes.Conflict, "应用角色编码已存在。");
        }

        var now = _timeProvider.GetUtcNow();
        var role = new AppRole(
            tenantId,
            appId,
            normalizedCode,
            request.Name.Trim(),
            request.Description,
            isSystem: false,
            operatorUserId,
            now,
            _idGeneratorAccessor.NextId());
        await _appRoleRepository.AddAsync(role, cancellationToken);
        await ReplaceRolePermissionsAsync(tenantId, appId, role.Id, request.PermissionCodes, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            ResolveActor(operatorUserId),
            "Platform.AppRole.Created",
            $"appId={appId};roleId={role.Id};code={normalizedCode};permissions={string.Join(',', request.PermissionCodes)}",
            cancellationToken);
        return role.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        long operatorUserId,
        TenantAppRoleUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedRoles(app);

        var role = await _appRoleRepository.FindByIdAsync(tenantId, appId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用角色不存在。");
        }

        if (role.IsSystem)
        {
            throw new BusinessException(ErrorCodes.Forbidden, "系统内置应用角色不允许修改。");
        }

        role.Update(request.Name.Trim(), request.Description, operatorUserId, _timeProvider.GetUtcNow());
        await _appRoleRepository.UpdateAsync(role, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            ResolveActor(operatorUserId),
            "Platform.AppRole.Updated",
            $"appId={appId};roleId={roleId};name={request.Name}",
            cancellationToken);
    }

    public async Task UpdatePermissionsAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        TenantAppRoleAssignPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedRoles(app);

        var role = await _appRoleRepository.FindByIdAsync(tenantId, appId, roleId, cancellationToken);
        if (role is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用角色不存在。");
        }

        await ReplaceRolePermissionsAsync(tenantId, appId, roleId, request.PermissionCodes, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            ResolveActor(),
            "Platform.AppRole.PermissionsUpdated",
            $"appId={appId};roleId={roleId};permissions={string.Join(',', request.PermissionCodes)}",
            cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);
        EnsureDedicatedRoles(app);

        var role = await _appRoleRepository.FindByIdAsync(tenantId, appId, roleId, cancellationToken);
        if (role is null)
        {
            return;
        }

        if (role.IsSystem)
        {
            throw new BusinessException(ErrorCodes.Forbidden, "系统内置应用角色不允许删除。");
        }

        await _appUserRoleRepository.DeleteByRoleIdAsync(tenantId, appId, roleId, cancellationToken);
        await _appRolePermissionRepository.DeleteByRoleIdAsync(tenantId, appId, roleId, cancellationToken);
        await _appRoleRepository.DeleteAsync(tenantId, appId, roleId, cancellationToken);
        await WriteAuditAsync(
            tenantId,
            ResolveActor(),
            "Platform.AppRole.Deleted",
            $"appId={appId};roleId={roleId};code={role.Code}",
            cancellationToken);
    }

    private async Task ReplaceRolePermissionsAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = permissionCodes
            .Select(code => code?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await _appRolePermissionRepository.DeleteByRoleIdAsync(tenantId, appId, roleId, cancellationToken);
        if (normalizedCodes.Length == 0)
        {
            return;
        }

        var entities = normalizedCodes
            .Select(code => new AppRolePermission(
                tenantId,
                appId,
                roleId,
                code,
                _idGeneratorAccessor.NextId()))
            .ToArray();
        await _appRolePermissionRepository.AddRangeAsync(entities, cancellationToken);
    }

    private async Task<Atlas.Domain.LowCode.Entities.LowCodeApp> RequireAppAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        var app = await _lowCodeAppRepository.GetByIdAsync(tenantId, appId, cancellationToken);
        if (app is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用实例不存在。");
        }

        return app;
    }

    private static void EnsureDedicatedRoles(Atlas.Domain.LowCode.Entities.LowCodeApp app)
    {
        if (app.UseSharedRoles)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前应用启用了共享角色，无法维护应用角色。");
        }
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    private string ResolveActor(long? fallbackUserId = null)
    {
        var current = _currentUserAccessor.GetCurrentUser();
        if (current is not null)
        {
            return current.Username;
        }

        return fallbackUserId.HasValue && fallbackUserId.Value > 0
            ? fallbackUserId.Value.ToString()
            : "system";
    }

    private async Task WriteAuditAsync(
        TenantId tenantId,
        string actor,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var record = new AuditRecord(
            tenantId,
            actor,
            action,
            "Success",
            target,
            null,
            null);
        await _auditWriter.WriteAsync(record, cancellationToken);
    }
}
