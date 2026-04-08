using Atlas.Application.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Identity.Repositories;
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
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class TenantAppMemberQueryService : ITenantAppMemberQueryService
{
    private readonly ILowCodeAppRepository _lowCodeAppRepository;
    private readonly IAppMemberRepository _appMemberRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IUserPositionRepository _userPositionRepository;
    private readonly IAppDepartmentRepository _appDepartmentRepository;
    private readonly IAppPositionRepository _appPositionRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IAppProjectRepository _appProjectRepository;

    public TenantAppMemberQueryService(
        ILowCodeAppRepository lowCodeAppRepository,
        IAppMemberRepository appMemberRepository,
        IUserAccountRepository userAccountRepository,
        IAppUserRoleRepository appUserRoleRepository,
        IAppRoleRepository appRoleRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IUserPositionRepository userPositionRepository,
        IAppDepartmentRepository appDepartmentRepository,
        IAppPositionRepository appPositionRepository,
        IProjectUserRepository projectUserRepository,
        IAppProjectRepository appProjectRepository)
    {
        _lowCodeAppRepository = lowCodeAppRepository;
        _appMemberRepository = appMemberRepository;
        _userAccountRepository = userAccountRepository;
        _appUserRoleRepository = appUserRoleRepository;
        _appRoleRepository = appRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _userPositionRepository = userPositionRepository;
        _appDepartmentRepository = appDepartmentRepository;
        _appPositionRepository = appPositionRepository;
        _projectUserRepository = projectUserRepository;
        _appProjectRepository = appProjectRepository;
    }

    public async Task<PagedResult<TenantAppMemberListItem>> QueryAsync(
        TenantId tenantId,
        long appId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);


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

        var items = await BuildMemberListItemsAsync(tenantId, appId, members, cancellationToken);

        return new PagedResult<TenantAppMemberListItem>(items, totalCount, pageIndex, pageSize);
    }

    public async Task<PagedResult<TenantAppMemberListItem>> QueryByRoleAsync(
        TenantId tenantId,
        long appId,
        long roleId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);

        var roleMappings = await _appUserRoleRepository.QueryByRoleIdsAsync(
            tenantId,
            appId,
            new[] { roleId },
            cancellationToken);
        var roleUserIds = roleMappings.Select(x => x.UserId).Distinct().ToArray();
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        if (roleUserIds.Length == 0)
        {
            return new PagedResult<TenantAppMemberListItem>(Array.Empty<TenantAppMemberListItem>(), 0, pageIndex, pageSize);
        }

        var roleMembers = await _appMemberRepository.QueryByUserIdsAsync(tenantId, appId, roleUserIds, cancellationToken);
        if (roleMembers.Count == 0)
        {
            return new PagedResult<TenantAppMemberListItem>(Array.Empty<TenantAppMemberListItem>(), 0, pageIndex, pageSize);
        }

        var keyword = request.Keyword?.Trim();
        IReadOnlyList<AppMember> filteredMembers = roleMembers;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var users = await _userAccountRepository.QueryByIdsAsync(
                tenantId,
                roleMembers.Select(x => x.UserId).Distinct().ToArray(),
                cancellationToken);
            var userMap = users.ToDictionary(x => x.Id);
            filteredMembers = roleMembers
                .Where(member =>
                {
                    var key = keyword!;
                    var byUserId = member.UserId.ToString().Contains(key, StringComparison.OrdinalIgnoreCase);
                    if (byUserId)
                    {
                        return true;
                    }

                    if (!userMap.TryGetValue(member.UserId, out var user))
                    {
                        return false;
                    }

                    return (!string.IsNullOrWhiteSpace(user.Username) && user.Username.Contains(key, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(user.DisplayName) && user.DisplayName.Contains(key, StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();
        }

        var totalCount = filteredMembers.Count;
        var pageMembers = filteredMembers
            .OrderByDescending(x => x.JoinedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToArray();
        if (pageMembers.Length == 0)
        {
            return new PagedResult<TenantAppMemberListItem>(Array.Empty<TenantAppMemberListItem>(), totalCount, pageIndex, pageSize);
        }

        var items = await BuildMemberListItemsAsync(tenantId, appId, pageMembers, cancellationToken);
        return new PagedResult<TenantAppMemberListItem>(items, totalCount, pageIndex, pageSize);
    }

    public async Task<PagedResult<TenantAppMemberListItem>> QueryByDepartmentAsync(
        TenantId tenantId,
        long appId,
        long departmentId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);

        var deptUserIds = (await _userDepartmentRepository.QueryUserIdsByDepartmentIdsAsync(
            tenantId,
            new[] { departmentId },
            cancellationToken)).Distinct().ToArray();
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        if (deptUserIds.Length == 0)
        {
            return new PagedResult<TenantAppMemberListItem>(Array.Empty<TenantAppMemberListItem>(), 0, pageIndex, pageSize);
        }

        var deptMembers = await _appMemberRepository.QueryByUserIdsAsync(tenantId, appId, deptUserIds, cancellationToken);
        if (deptMembers.Count == 0)
        {
            return new PagedResult<TenantAppMemberListItem>(Array.Empty<TenantAppMemberListItem>(), 0, pageIndex, pageSize);
        }

        var keyword = request.Keyword?.Trim();
        IReadOnlyList<AppMember> filteredMembers = deptMembers;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var users = await _userAccountRepository.QueryByIdsAsync(
                tenantId,
                deptMembers.Select(x => x.UserId).Distinct().ToArray(),
                cancellationToken);
            var userMap = users.ToDictionary(x => x.Id);
            filteredMembers = deptMembers
                .Where(member =>
                {
                    var key = keyword!;
                    if (!userMap.TryGetValue(member.UserId, out var user))
                    {
                        return false;
                    }

                    return (!string.IsNullOrWhiteSpace(user.Username) && user.Username.Contains(key, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(user.DisplayName) && user.DisplayName.Contains(key, StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();
        }

        var totalCount = filteredMembers.Count;
        var pageMembers = filteredMembers
            .OrderByDescending(x => x.JoinedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToArray();
        if (pageMembers.Length == 0)
        {
            return new PagedResult<TenantAppMemberListItem>(Array.Empty<TenantAppMemberListItem>(), totalCount, pageIndex, pageSize);
        }

        var items = await BuildMemberListItemsAsync(tenantId, appId, pageMembers, cancellationToken);
        return new PagedResult<TenantAppMemberListItem>(items, totalCount, pageIndex, pageSize);
    }

    private async Task<TenantAppMemberListItem[]> BuildMemberListItemsAsync(
        TenantId tenantId,
        long appId,
        IReadOnlyList<AppMember> members,
        CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return Array.Empty<TenantAppMemberListItem>();
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
        var userDepartments = await _userDepartmentRepository.QueryByUserIdsAsync(tenantId, userIds, cancellationToken);
        var appDepartments = await _appDepartmentRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appDepartmentMap = appDepartments.ToDictionary(x => x.Id);
        var departmentIdsByUser = userDepartments
            .Where(x => appDepartmentMap.ContainsKey(x.DepartmentId))
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.DepartmentId).Distinct().ToArray());
        var userPositions = await _userPositionRepository.QueryByUserIdsAsync(tenantId, userIds, cancellationToken);
        var appPositions = await _appPositionRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appPositionMap = appPositions.ToDictionary(x => x.Id);
        var positionIdsByUser = userPositions
            .Where(x => appPositionMap.ContainsKey(x.PositionId))
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.PositionId).Distinct().ToArray());
        var projectMappings = await _projectUserRepository.QueryByUserIdsAsync(tenantId, userIds, cancellationToken);
        var appProjects = await _appProjectRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appProjectMap = appProjects.ToDictionary(x => x.Id);
        var projectIdsByUser = projectMappings
            .Where(x => appProjectMap.ContainsKey(x.ProjectId))
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.ProjectId).Distinct().ToArray());

        return members
            .Select(member =>
            {
                userMap.TryGetValue(member.UserId, out var user);
                roleIdsByUser.TryGetValue(member.UserId, out var memberRoleIds);
                memberRoleIds ??= Array.Empty<long>();
                departmentIdsByUser.TryGetValue(member.UserId, out var memberDepartmentIds);
                memberDepartmentIds ??= Array.Empty<long>();
                positionIdsByUser.TryGetValue(member.UserId, out var memberPositionIds);
                memberPositionIds ??= Array.Empty<long>();
                projectIdsByUser.TryGetValue(member.UserId, out var memberProjectIds);
                memberProjectIds ??= Array.Empty<long>();
                var roleNames = memberRoleIds
                    .Select(itemRoleId => roleMap.TryGetValue(itemRoleId, out var role) ? role.Name : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToArray();
                var departmentNames = memberDepartmentIds
                    .Select(departmentId => appDepartmentMap.TryGetValue(departmentId, out var department) ? department.Name : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToArray();
                var positionNames = memberPositionIds
                    .Select(positionId => appPositionMap.TryGetValue(positionId, out var position) ? position.Name : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToArray();
                var projectNames = memberProjectIds
                    .Select(projectId => appProjectMap.TryGetValue(projectId, out var project) ? project.Name : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToArray();

                return new TenantAppMemberListItem(
                    member.UserId.ToString(),
                    user?.Username ?? member.UserId.ToString(),
                    user?.DisplayName ?? user?.Username ?? member.UserId.ToString(),
                    user?.Email,
                    user?.PhoneNumber,
                    user?.IsActive ?? false,
                    member.JoinedAt.ToString("O"),
                    memberRoleIds.Select(x => x.ToString()).ToArray(),
                    roleNames,
                    memberDepartmentIds.Select(x => x.ToString()).ToArray(),
                    departmentNames,
                    memberPositionIds.Select(x => x.ToString()).ToArray(),
                    positionNames,
                    memberProjectIds.Select(x => x.ToString()).ToArray(),
                    projectNames);
            })
            .ToArray();
    }

    public async Task<TenantAppMemberDetail?> GetByUserIdAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);


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
        var appDepartments = await _appDepartmentRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appDepartmentMap = appDepartments.ToDictionary(x => x.Id);
        var allUserDepartmentIds = (await _userDepartmentRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken))
            .Select(x => x.DepartmentId)
            .Distinct()
            .ToArray();
        var departmentIds = allUserDepartmentIds
            .Where(departmentId => appDepartmentMap.ContainsKey(departmentId))
            .ToArray();
        var departmentNames = departmentIds
            .Select(departmentId => appDepartmentMap.TryGetValue(departmentId, out var department) ? department.Name : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
        var appPositions = await _appPositionRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appPositionMap = appPositions.ToDictionary(x => x.Id);
        var allUserPositionIds = (await _userPositionRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken))
            .Select(x => x.PositionId)
            .Distinct()
            .ToArray();
        var positionIds = allUserPositionIds
            .Where(positionId => appPositionMap.ContainsKey(positionId))
            .ToArray();
        var positionNames = positionIds
            .Select(positionId => appPositionMap.TryGetValue(positionId, out var position) ? position.Name : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
        var allUserProjectIds = await _projectUserRepository.QueryProjectIdsByUserIdAsync(tenantId, userId, cancellationToken);
        var appProjects = await _appProjectRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appProjectMap = appProjects.ToDictionary(x => x.Id);
        var projectIds = allUserProjectIds
            .Where(projectId => appProjectMap.ContainsKey(projectId))
            .Distinct()
            .ToArray();
        var projectNames = projectIds
            .Select(projectId => appProjectMap.TryGetValue(projectId, out var project) ? project.Name : null)
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
            roleNames,
            departmentIds.Select(x => x.ToString()).ToArray(),
            departmentNames,
            positionIds.Select(x => x.ToString()).ToArray(),
            positionNames,
            projectIds.Select(x => x.ToString()).ToArray(),
            projectNames);
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

}

public sealed class TenantAppMemberCommandService : ITenantAppMemberCommandService
{
    private readonly ILowCodeAppRepository _lowCodeAppRepository;
    private readonly IAppMemberRepository _appMemberRepository;
    private readonly IAppRoleRepository _appRoleRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAppUserRoleRepository _appUserRoleRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IUserPositionRepository _userPositionRepository;
    private readonly IAppDepartmentRepository _appDepartmentRepository;
    private readonly IAppPositionRepository _appPositionRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IAppProjectRepository _appProjectRepository;
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
        IUserDepartmentRepository userDepartmentRepository,
        IUserPositionRepository userPositionRepository,
        IAppDepartmentRepository appDepartmentRepository,
        IAppPositionRepository appPositionRepository,
        IProjectUserRepository projectUserRepository,
        IAppProjectRepository appProjectRepository,
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
        _userDepartmentRepository = userDepartmentRepository;
        _userPositionRepository = userPositionRepository;
        _appDepartmentRepository = appDepartmentRepository;
        _appPositionRepository = appPositionRepository;
        _projectUserRepository = projectUserRepository;
        _appProjectRepository = appProjectRepository;
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
        var departmentIds = request.DepartmentIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<long>();
        var positionIds = request.PositionIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<long>();
        var projectIds = request.ProjectIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<long>();
        if (roleIds.Length > 0)
        {

            var roles = await _appRoleRepository.QueryByIdsAsync(tenantId, appId, roleIds, cancellationToken);
            if (roles.Count != roleIds.Length)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用角色。");
            }
        }
        if (projectIds.Length > 0)
        {
            var appProjects = await _appProjectRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
            var appProjectIdSet = appProjects.Select(x => x.Id).ToHashSet();
            if (projectIds.Any(projectId => !appProjectIdSet.Contains(projectId)))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用项目。");
            }
        }
        if (departmentIds.Length > 0)
        {
            var appDepartments = await _appDepartmentRepository.QueryByIdsAsync(tenantId, appId, departmentIds, cancellationToken);
            if (appDepartments.Count != departmentIds.Length)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用部门。");
            }
        }
        if (positionIds.Length > 0)
        {
            var appPositions = await _appPositionRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
            var appPositionIdSet = appPositions.Select(x => x.Id).ToHashSet();
            if (positionIds.Any(positionId => !appPositionIdSet.Contains(positionId)))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用职位。");
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

        if (roleIds.Length > 0)
        {
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
        }
        if (projectIds.Length > 0)
        {
            var existingProjectMappings = await _projectUserRepository.QueryByUserIdsAsync(tenantId, userIds, cancellationToken);
            var existingProjectMappingSet = existingProjectMappings
                .Select(x => $"{x.UserId}:{x.ProjectId}")
                .ToHashSet(StringComparer.Ordinal);
            var projectMappingsToAdd = new List<ProjectUser>(userIds.Length * projectIds.Length);
            foreach (var userId in userIds)
            {
                foreach (var projectId in projectIds)
                {
                    var key = $"{userId}:{projectId}";
                    if (!existingProjectMappingSet.Contains(key))
                    {
                        projectMappingsToAdd.Add(new ProjectUser(
                            tenantId,
                            projectId,
                            userId,
                            _idGeneratorAccessor.NextId()));
                    }
                }
            }

            await _projectUserRepository.AddRangeAsync(projectMappingsToAdd, cancellationToken);
        }
        if (departmentIds.Length > 0)
        {
            var existingDepartmentMappings = await _userDepartmentRepository.QueryByUserIdsAsync(tenantId, userIds, cancellationToken);
            var existingDepartmentMappingSet = existingDepartmentMappings
                .Select(x => $"{x.UserId}:{x.DepartmentId}")
                .ToHashSet(StringComparer.Ordinal);
            var departmentMappingsToAdd = new List<UserDepartment>(userIds.Length * departmentIds.Length);
            foreach (var userId in userIds)
            {
                foreach (var departmentId in departmentIds)
                {
                    var key = $"{userId}:{departmentId}";
                    if (!existingDepartmentMappingSet.Contains(key))
                    {
                        departmentMappingsToAdd.Add(new UserDepartment(
                            tenantId,
                            userId,
                            departmentId,
                            _idGeneratorAccessor.NextId(),
                            false));
                    }
                }
            }

            await _userDepartmentRepository.AddRangeAsync(departmentMappingsToAdd, cancellationToken);
        }
        if (positionIds.Length > 0)
        {
            var existingPositionMappings = await _userPositionRepository.QueryByUserIdsAsync(tenantId, userIds, cancellationToken);
            var existingPositionMappingSet = existingPositionMappings
                .Select(x => $"{x.UserId}:{x.PositionId}")
                .ToHashSet(StringComparer.Ordinal);
            var positionMappingsToAdd = new List<UserPosition>(userIds.Length * positionIds.Length);
            foreach (var userId in userIds)
            {
                foreach (var positionId in positionIds)
                {
                    var key = $"{userId}:{positionId}";
                    if (!existingPositionMappingSet.Contains(key))
                    {
                        positionMappingsToAdd.Add(new UserPosition(
                            tenantId,
                            userId,
                            positionId,
                            _idGeneratorAccessor.NextId(),
                            false));
                    }
                }
            }

            await _userPositionRepository.AddRangeAsync(positionMappingsToAdd, cancellationToken);
        }

        await WriteAuditAsync(
            tenantId,
            ResolveActor(operatorUserId),
            "Platform.AppMember.Assigned",
            $"appId={appId};userIds={string.Join(',', userIds)};roleIds={string.Join(',', roleIds)};departmentIds={string.Join(',', departmentIds)};positionIds={string.Join(',', positionIds)};projectIds={string.Join(',', projectIds)}",
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



        var exists = await _appMemberRepository.ExistsAsync(tenantId, appId, userId, cancellationToken);
        if (!exists)
        {
            throw new BusinessException(ErrorCodes.NotFound, "应用成员不存在。");
        }

        var roleIds = request.RoleIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var departmentIds = request.DepartmentIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<long>();
        var positionIds = request.PositionIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<long>();
        var projectIds = request.ProjectIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray() ?? Array.Empty<long>();
        if (roleIds.Length > 0)
        {
            var roles = await _appRoleRepository.QueryByIdsAsync(tenantId, appId, roleIds, cancellationToken);
            if (roles.Count != roleIds.Length)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用角色。");
            }
        }
        if (projectIds.Length > 0)
        {
            var appProjects = await _appProjectRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
            var appProjectIdSet = appProjects.Select(x => x.Id).ToHashSet();
            if (projectIds.Any(projectId => !appProjectIdSet.Contains(projectId)))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用项目。");
            }
        }
        if (departmentIds.Length > 0)
        {
            var appDepartments = await _appDepartmentRepository.QueryByIdsAsync(tenantId, appId, departmentIds, cancellationToken);
            if (appDepartments.Count != departmentIds.Length)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用部门。");
            }
        }
        if (positionIds.Length > 0)
        {
            var appPositions = await _appPositionRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
            var appPositionIdSet = appPositions.Select(x => x.Id).ToHashSet();
            if (positionIds.Any(positionId => !appPositionIdSet.Contains(positionId)))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "存在无效的应用职位。");
            }
        }

        await _appUserRoleRepository.DeleteByUserIdAsync(tenantId, appId, userId, cancellationToken);
        if (roleIds.Length > 0)
        {
            var mappings = roleIds
                .Select(roleId => new AppUserRole(
                    tenantId,
                    appId,
                    userId,
                    roleId,
                    _idGeneratorAccessor.NextId()))
                .ToArray();
            await _appUserRoleRepository.AddRangeAsync(mappings, cancellationToken);
        }
        var allAppProjects = await _appProjectRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var allAppProjectIdSet = allAppProjects.Select(x => x.Id).ToHashSet();
        var existingProjectIdsByUser = await _projectUserRepository.QueryProjectIdsByUserIdAsync(tenantId, userId, cancellationToken);
        var existingAppProjectIds = existingProjectIdsByUser
            .Where(projectId => allAppProjectIdSet.Contains(projectId))
            .Distinct()
            .ToArray();
        var projectIdsToDelete = existingAppProjectIds
            .Where(existingProjectId => !projectIds.Contains(existingProjectId))
            .ToArray();
        if (projectIdsToDelete.Length > 0)
        {
            await _projectUserRepository.DeleteByUserAndProjectIdsAsync(tenantId, userId, projectIdsToDelete, cancellationToken);
        }

        var projectIdSet = existingAppProjectIds.ToHashSet();
        var projectMappingsToAdd = projectIds
            .Where(projectId => !projectIdSet.Contains(projectId))
            .Select(projectId => new ProjectUser(
                tenantId,
                projectId,
                userId,
                _idGeneratorAccessor.NextId()))
            .ToArray();
        await _projectUserRepository.AddRangeAsync(projectMappingsToAdd, cancellationToken);
        var allAppDepartments = await _appDepartmentRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var allAppDepartmentIdSet = allAppDepartments.Select(x => x.Id).ToHashSet();
        var existingDepartmentIdsByUser = (await _userDepartmentRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken))
            .Select(x => x.DepartmentId)
            .Distinct()
            .ToArray();
        var existingAppDepartmentIds = existingDepartmentIdsByUser
            .Where(departmentId => allAppDepartmentIdSet.Contains(departmentId))
            .ToArray();
        var departmentIdsToDelete = existingAppDepartmentIds
            .Where(existingDepartmentId => !departmentIds.Contains(existingDepartmentId))
            .ToArray();
        if (departmentIdsToDelete.Length > 0)
        {
            await _userDepartmentRepository.DeleteByUserAndDepartmentIdsAsync(tenantId, userId, departmentIdsToDelete, cancellationToken);
        }

        var departmentIdSet = existingAppDepartmentIds.ToHashSet();
        var departmentMappingsToAdd = departmentIds
            .Where(departmentId => !departmentIdSet.Contains(departmentId))
            .Select(departmentId => new UserDepartment(
                tenantId,
                userId,
                departmentId,
                _idGeneratorAccessor.NextId(),
                false))
            .ToArray();
        await _userDepartmentRepository.AddRangeAsync(departmentMappingsToAdd, cancellationToken);
        var allAppPositions = await _appPositionRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var allAppPositionIdSet = allAppPositions.Select(x => x.Id).ToHashSet();
        var existingPositionIdsByUser = (await _userPositionRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken))
            .Select(x => x.PositionId)
            .Distinct()
            .ToArray();
        var existingAppPositionIds = existingPositionIdsByUser
            .Where(positionId => allAppPositionIdSet.Contains(positionId))
            .ToArray();
        var positionIdsToDelete = existingAppPositionIds
            .Where(existingPositionId => !positionIds.Contains(existingPositionId))
            .ToArray();
        if (positionIdsToDelete.Length > 0)
        {
            await _userPositionRepository.DeleteByUserAndPositionIdsAsync(tenantId, userId, positionIdsToDelete, cancellationToken);
        }

        var positionIdSet = existingAppPositionIds.ToHashSet();
        var positionMappingsToAdd = positionIds
            .Where(positionId => !positionIdSet.Contains(positionId))
            .Select(positionId => new UserPosition(
                tenantId,
                userId,
                positionId,
                _idGeneratorAccessor.NextId(),
                false))
            .ToArray();
        await _userPositionRepository.AddRangeAsync(positionMappingsToAdd, cancellationToken);

        await WriteAuditAsync(
            tenantId,
            ResolveActor(),
            "Platform.AppMember.RolesUpdated",
            $"appId={appId};userId={userId};roleIds={string.Join(',', roleIds)};departmentIds={string.Join(',', departmentIds)};positionIds={string.Join(',', positionIds)};projectIds={string.Join(',', projectIds)}",
            cancellationToken);
    }

    public async Task RemoveMemberAsync(
        TenantId tenantId,
        long appId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);


        await _appUserRoleRepository.DeleteByUserIdAsync(tenantId, appId, userId, cancellationToken);
        var appDepartments = await _appDepartmentRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appDepartmentIds = appDepartments.Select(department => department.Id).ToArray();
        if (appDepartmentIds.Length > 0)
        {
            await _userDepartmentRepository.DeleteByUserAndDepartmentIdsAsync(tenantId, userId, appDepartmentIds, cancellationToken);
        }
        var appPositions = await _appPositionRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appPositionIds = appPositions.Select(position => position.Id).ToArray();
        if (appPositionIds.Length > 0)
        {
            await _userPositionRepository.DeleteByUserAndPositionIdsAsync(tenantId, userId, appPositionIds, cancellationToken);
        }
        var appProjects = await _appProjectRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appProjectIds = appProjects.Select(project => project.Id).ToArray();
        if (appProjectIds.Length > 0)
        {
            await _projectUserRepository.DeleteByUserAndProjectIdsAsync(tenantId, userId, appProjectIds, cancellationToken);
        }
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
        bool? isSystem = null,
        CancellationToken cancellationToken = default)
    {
        var app = await RequireAppAsync(tenantId, appId, cancellationToken);


        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var (roles, totalCount) = await _appRoleRepository.QueryPageAsync(
            tenantId,
            appId,
            pageIndex,
            pageSize,
            request.Keyword,
            isSystem,
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
