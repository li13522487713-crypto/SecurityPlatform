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
    private readonly IMapper _mapper;

    public ProjectQueryService(
        IProjectRepository projectRepository,
        IProjectUserRepository projectUserRepository,
        IProjectDepartmentRepository projectDepartmentRepository,
        IProjectPositionRepository projectPositionRepository,
        IMapper mapper)
    {
        _projectRepository = projectRepository;
        _projectUserRepository = projectUserRepository;
        _projectDepartmentRepository = projectDepartmentRepository;
        _projectPositionRepository = projectPositionRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<ProjectListItem>> QueryProjectsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _projectRepository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<ProjectListItem>(x)).ToArray();
        return new PagedResult<ProjectListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<ProjectDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var userIds = await _projectUserRepository.QueryByProjectIdAsync(tenantId, id, cancellationToken);
        var departmentIds = await _projectDepartmentRepository.QueryByProjectIdAsync(tenantId, id, cancellationToken);
        var positionIds = await _projectPositionRepository.QueryByProjectIdAsync(tenantId, id, cancellationToken);

        return new ProjectDetail(
            project.Id.ToString(),
            project.Code,
            project.Name,
            project.IsActive,
            project.Description,
            project.SortOrder,
            userIds.Select(x => x.UserId).ToArray(),
            departmentIds.Select(x => x.DepartmentId).ToArray(),
            positionIds.Select(x => x.PositionId).ToArray());
    }

    public async Task<IReadOnlyList<ProjectListItem>> QueryMyProjectsAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var projectIds = await _projectUserRepository.QueryProjectIdsByUserIdAsync(
            tenantId,
            userId,
            cancellationToken);
        if (projectIds.Count == 0)
        {
            return Array.Empty<ProjectListItem>();
        }

        var projects = await _projectRepository.QueryByIdsAsync(tenantId, projectIds.Distinct().ToArray(), cancellationToken);
        return projects.Select(x => _mapper.Map<ProjectListItem>(x)).ToArray();
    }
}
