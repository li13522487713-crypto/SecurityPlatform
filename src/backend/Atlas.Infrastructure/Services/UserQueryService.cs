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
    private readonly IMapper _mapper;

    public UserQueryService(
        IUserAccountRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IUserPositionRepository userPositionRepository,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _userPositionRepository = userPositionRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserListItem>> QueryUsersAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _userRepository.QueryPageAsync(
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<UserListItem>(x)).ToArray();
        return new PagedResult<UserListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<UserDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
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
