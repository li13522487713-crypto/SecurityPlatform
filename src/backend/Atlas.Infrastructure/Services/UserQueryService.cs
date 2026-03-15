using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class UserQueryService : IUserQueryService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IUserPositionRepository _userPositionRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly Atlas.Core.Identity.IProjectContextAccessor _projectContextAccessor;
    private readonly IDataScopeFilter _dataScopeFilter;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IMapper _mapper;

    public UserQueryService(
        IUserAccountRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IUserPositionRepository userPositionRepository,
        IProjectUserRepository projectUserRepository,
        Atlas.Core.Identity.IProjectContextAccessor projectContextAccessor,
        IDataScopeFilter dataScopeFilter,
        ICurrentUserAccessor currentUserAccessor,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _userPositionRepository = userPositionRepository;
        _projectUserRepository = projectUserRepository;
        _projectContextAccessor = projectContextAccessor;
        _dataScopeFilter = dataScopeFilter;
        _currentUserAccessor = currentUserAccessor;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserListItem>> QueryUsersAsync(
        UserQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var projectContext = _projectContextAccessor.GetCurrent();
        (IReadOnlyList<Atlas.Domain.Identity.Entities.UserAccount> Items, int TotalCount) result;
        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            var userIds = await _projectUserRepository.QueryUserIdsByProjectIdAsync(
                tenantId,
                projectContext.ProjectId.Value,
                cancellationToken);
            result = await _userRepository.QueryPageByIdsAsync(
                tenantId,
                userIds.Distinct().ToArray(),
                pageIndex,
                pageSize,
                request.Keyword,
                cancellationToken);
        }
        else
        {
            result = await _userRepository.QueryPageAsync(
                tenantId,
                pageIndex,
                pageSize,
                request.Keyword,
                cancellationToken);
        }

        var ownerFilterId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        var deptFilterIds = await _dataScopeFilter.GetDeptFilterIdsAsync(cancellationToken);
        var projectFilterIds = await _dataScopeFilter.GetProjectFilterIdsAsync(cancellationToken);
        var scopedItems = result.Items.AsEnumerable();
        if (ownerFilterId.HasValue)
        {
            scopedItems = scopedItems.Where(x => x.Id == ownerFilterId.Value);
        }
        if (deptFilterIds is { Count: > 0 })
        {
            var deptSet = deptFilterIds.ToHashSet();
            var mappings = await _userDepartmentRepository.QueryByUserIdsAsync(
                tenantId,
                result.Items.Select(x => x.Id).Distinct().ToArray(),
                cancellationToken);
            var allowedUserIds = mappings
                .Where(x => deptSet.Contains(x.DepartmentId))
                .Select(x => x.UserId)
                .ToHashSet();
            scopedItems = scopedItems.Where(x => allowedUserIds.Contains(x.Id));
        }
        if (projectFilterIds is { Count: > 0 })
        {
            var projectSet = projectFilterIds.ToHashSet();
            var userProjectMappings = await _projectUserRepository.QueryByUserIdsAsync(
                tenantId,
                result.Items.Select(x => x.Id).Distinct().ToArray(),
                cancellationToken);
            var allowedUserIds = userProjectMappings
                .Where(x => projectSet.Contains(x.ProjectId))
                .Select(x => x.UserId)
                .ToHashSet();
            scopedItems = scopedItems.Where(x => allowedUserIds.Contains(x.Id));
        }
        var scopedArray = scopedItems.ToArray();

        if (request.DepartmentId.HasValue)
        {
            var deptUserMappings = await _userDepartmentRepository.QueryByUserIdsAsync(
                tenantId,
                scopedArray.Select(x => x.Id).Distinct().ToArray(),
                cancellationToken);
            var deptUserIds = deptUserMappings
                .Where(x => x.DepartmentId == request.DepartmentId.Value)
                .Select(x => x.UserId)
                .ToHashSet();
            scopedArray = scopedArray.Where(x => deptUserIds.Contains(x.Id)).ToArray();
        }

        var resultItems = scopedArray.Select(x => _mapper.Map<UserListItem>(x)).ToArray();
        var scopedTotal = resultItems.Length;

        return new PagedResult<UserListItem>(resultItems, scopedTotal, pageIndex, pageSize);
    }

    public async Task<UserDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var projectContext = _projectContextAccessor.GetCurrent();
        var ownerFilterId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        var deptFilterIds = await _dataScopeFilter.GetDeptFilterIdsAsync(cancellationToken);
        var projectFilterIds = await _dataScopeFilter.GetProjectFilterIdsAsync(cancellationToken);
        if (ownerFilterId.HasValue && ownerFilterId.Value != id)
        {
            return null;
        }

        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            var hasMembership = await _projectUserRepository.ExistsAsync(
                tenantId,
                projectContext.ProjectId.Value,
                id,
                cancellationToken);
            if (!hasMembership)
            {
                return null;
            }
        }

        var user = await _userRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // 二次防御：当数据范围为仅本人时，只允许读取当前用户明细
        if (ownerFilterId.HasValue)
        {
            var currentUser = _currentUserAccessor.GetCurrentUser();
            if (currentUser is null || currentUser.UserId != user.Id)
            {
                return null;
            }
        }

        var roleIds = await _userRoleRepository.QueryByUserIdAsync(tenantId, id, cancellationToken);
        var departmentIds = await _userDepartmentRepository.QueryByUserIdAsync(tenantId, id, cancellationToken);
        var positionIds = await _userPositionRepository.QueryByUserIdAsync(tenantId, id, cancellationToken);
        if (deptFilterIds is { Count: > 0 } && !departmentIds.Any(x => deptFilterIds.Contains(x.DepartmentId)))
        {
            return null;
        }
        if (projectFilterIds is { Count: > 0 })
        {
            var myProjectIds = await _projectUserRepository.QueryProjectIdsByUserIdAsync(tenantId, id, cancellationToken);
            if (!myProjectIds.Any(projectFilterIds.Contains))
            {
                return null;
            }
        }

        return new UserDetail(
            user.Id.ToString(),
            user.Username,
            user.DisplayName,
            user.Email,
            user.PhoneNumber,
            user.IsActive,
            user.IsSystem,
            user.LastLoginAt,
            roleIds.Select(x => x.RoleId).ToArray(),
            departmentIds.Select(x => x.DepartmentId).ToArray(),
            positionIds.Select(x => x.PositionId).ToArray());
    }
}
