using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class ProjectQueryService : IProjectQueryService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IProjectDepartmentRepository _projectDepartmentRepository;
    private readonly IProjectPositionRepository _projectPositionRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly ITenantDataScopeFilter _dataScopeFilter;
    private readonly IMapper _mapper;

    public ProjectQueryService(
        IProjectRepository projectRepository,
        IProjectUserRepository projectUserRepository,
        IProjectDepartmentRepository projectDepartmentRepository,
        IProjectPositionRepository projectPositionRepository,
        IUserDepartmentRepository userDepartmentRepository,
        ITenantDataScopeFilter dataScopeFilter,
        IMapper mapper)
    {
        _projectRepository = projectRepository;
        _projectUserRepository = projectUserRepository;
        _projectDepartmentRepository = projectDepartmentRepository;
        _projectPositionRepository = projectPositionRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _dataScopeFilter = dataScopeFilter;
        _mapper = mapper;
    }

    public async Task<PagedResult<ProjectListItem>> QueryProjectsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var restrictIds = await ResolveRestrictProjectIdsAsync(tenantId, cancellationToken);

        var (items, total) = await _projectRepository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            restrictIds,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<ProjectListItem>(x)).ToArray();
        return new PagedResult<ProjectListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<ProjectDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var restrictIds = await ResolveRestrictProjectIdsAsync(tenantId, cancellationToken);
        if (restrictIds is not null && !restrictIds.Contains(id))
        {
            return null;
        }

        var project = await _projectRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var userIdsTask = _projectUserRepository.QueryByProjectIdAsync(tenantId, id, cancellationToken);
        var departmentIdsTask = _projectDepartmentRepository.QueryByProjectIdAsync(tenantId, id, cancellationToken);
        var positionIdsTask = _projectPositionRepository.QueryByProjectIdAsync(tenantId, id, cancellationToken);
        await Task.WhenAll(userIdsTask, departmentIdsTask, positionIdsTask);

        return new ProjectDetail(
            project.Id.ToString(),
            project.Code,
            project.Name,
            project.IsActive,
            project.Description,
            project.SortOrder,
            userIdsTask.Result.Select(x => x.UserId).ToArray(),
            departmentIdsTask.Result.Select(x => x.DepartmentId).ToArray(),
            positionIdsTask.Result.Select(x => x.PositionId).ToArray());
    }

    public async Task<IReadOnlyList<ProjectListItem>> QueryMyProjectsAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var restrictIds = await ResolveRestrictProjectIdsAsync(tenantId, cancellationToken);
        var projectIds = await _projectUserRepository.QueryProjectIdsByUserIdAsync(
            tenantId,
            userId,
            cancellationToken);
        if (projectIds.Count == 0)
        {
            return Array.Empty<ProjectListItem>();
        }

        var distinctIds = projectIds.Distinct().ToArray();
        if (restrictIds is not null)
        {
            var allowed = restrictIds.ToHashSet();
            distinctIds = distinctIds.Where(id => allowed.Contains(id)).ToArray();
            if (distinctIds.Length == 0)
            {
                return Array.Empty<ProjectListItem>();
            }
        }

        var projects = await _projectRepository.QueryByIdsAsync(tenantId, distinctIds, cancellationToken);
        return projects.Select(x => _mapper.Map<ProjectListItem>(x)).ToArray();
    }

    public async Task<PagedResult<ProjectListItem>> QueryMyProjectsPagedAsync(
        PagedRequest request,
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var restrictIds = await ResolveRestrictProjectIdsAsync(tenantId, cancellationToken);

        var (projects, total) = await _projectRepository.QueryPagedByUserIdAsync(
            tenantId,
            userId,
            pageIndex,
            pageSize,
            request.Keyword,
            restrictIds,
            cancellationToken);
        var items = projects
            .Select(x => _mapper.Map<ProjectListItem>(x))
            .ToArray();

        return new PagedResult<ProjectListItem>(items, total, pageIndex, pageSize);
    }

    /// <summary>
    /// 按数据权限解析允许访问的项目 ID 集合；null 表示不限制。
    /// </summary>
    private async Task<long[]?> ResolveRestrictProjectIdsAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var projectScopeIds = await _dataScopeFilter.GetProjectFilterIdsAsync(cancellationToken);
        if (projectScopeIds is not null)
        {
            return projectScopeIds.Count == 0
                ? Array.Empty<long>()
                : projectScopeIds.Distinct().ToArray();
        }

        var ownerId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        if (ownerId.HasValue)
        {
            var ids = await _projectUserRepository.QueryProjectIdsByUserIdAsync(
                tenantId,
                ownerId.Value,
                cancellationToken);
            return ids.Distinct().ToArray();
        }

        var deptIds = await _dataScopeFilter.GetDeptFilterIdsAsync(cancellationToken);
        if (deptIds is not null)
        {
            if (deptIds.Count == 0)
            {
                return Array.Empty<long>();
            }

            var byDeptLink = await _projectDepartmentRepository.QueryProjectIdsByDepartmentIdsAsync(
                tenantId,
                deptIds,
                cancellationToken);
            var userIds = await _userDepartmentRepository.QueryUserIdsByDepartmentIdsAsync(
                tenantId,
                deptIds,
                cancellationToken);
            long[] byMembership;
            if (userIds.Count == 0)
            {
                byMembership = Array.Empty<long>();
            }
            else
            {
                var rows = await _projectUserRepository.QueryByUserIdsAsync(tenantId, userIds, cancellationToken);
                byMembership = rows.Select(x => x.ProjectId).Distinct().ToArray();
            }

            return byDeptLink.Union(byMembership).Distinct().ToArray();
        }

        return null;
    }
}
