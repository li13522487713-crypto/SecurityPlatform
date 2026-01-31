using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
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
    private readonly IMapper _mapper;

    public UserQueryService(
        IUserAccountRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IUserPositionRepository userPositionRepository,
        IProjectUserRepository projectUserRepository,
        Atlas.Core.Identity.IProjectContextAccessor projectContextAccessor,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _userPositionRepository = userPositionRepository;
        _projectUserRepository = projectUserRepository;
        _projectContextAccessor = projectContextAccessor;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserListItem>> QueryUsersAsync(
        PagedRequest request,
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

        var resultItems = result.Items.Select(x => _mapper.Map<UserListItem>(x)).ToArray();
        return new PagedResult<UserListItem>(resultItems, result.TotalCount, pageIndex, pageSize);
    }

    public async Task<UserDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var projectContext = _projectContextAccessor.GetCurrent();
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

        var roleIds = await _userRoleRepository.QueryByUserIdAsync(tenantId, id, cancellationToken);
        var departmentIds = await _userDepartmentRepository.QueryByUserIdAsync(tenantId, id, cancellationToken);
        var positionIds = await _userPositionRepository.QueryByUserIdAsync(tenantId, id, cancellationToken);

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
