using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class DepartmentQueryService : IDepartmentQueryService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IProjectDepartmentRepository _projectDepartmentRepository;
    private readonly Atlas.Core.Identity.IProjectContextAccessor _projectContextAccessor;
    private readonly IMapper _mapper;

    public DepartmentQueryService(
        IDepartmentRepository departmentRepository,
        IProjectDepartmentRepository projectDepartmentRepository,
        Atlas.Core.Identity.IProjectContextAccessor projectContextAccessor,
        IMapper mapper)
    {
        _departmentRepository = departmentRepository;
        _projectDepartmentRepository = projectDepartmentRepository;
        _projectContextAccessor = projectContextAccessor;
        _mapper = mapper;
    }

    public async Task<PagedResult<DepartmentListItem>> QueryDepartmentsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var projectContext = _projectContextAccessor.GetCurrent();
        (IReadOnlyList<Atlas.Domain.Identity.Entities.Department> Items, int TotalCount) result;
        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            var relations = await _projectDepartmentRepository.QueryByProjectIdAsync(
                tenantId,
                projectContext.ProjectId.Value,
                cancellationToken);
            var departmentIds = relations.Select(x => x.DepartmentId).Distinct().ToArray();
            result = await _departmentRepository.QueryPageByIdsAsync(
                tenantId,
                departmentIds,
                pageIndex,
                pageSize,
                request.Keyword,
                cancellationToken);
        }
        else
        {
            result = await _departmentRepository.QueryPageAsync(
                tenantId,
                pageIndex,
                pageSize,
                request.Keyword,
                cancellationToken);
        }

        var resultItems = result.Items.Select(x => _mapper.Map<DepartmentListItem>(x)).ToArray();
        return new PagedResult<DepartmentListItem>(resultItems, result.TotalCount, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<DepartmentListItem>> QueryAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var projectContext = _projectContextAccessor.GetCurrent();
        IReadOnlyList<Atlas.Domain.Identity.Entities.Department> items;
        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            var relations = await _projectDepartmentRepository.QueryByProjectIdAsync(
                tenantId,
                projectContext.ProjectId.Value,
                cancellationToken);
            var departmentIds = relations.Select(x => x.DepartmentId).Distinct().ToArray();
            items = await _departmentRepository.QueryByIdsAsync(tenantId, departmentIds, cancellationToken);
        }
        else
        {
            items = await _departmentRepository.QueryAllAsync(tenantId, cancellationToken);
        }

        return items.Select(x => _mapper.Map<DepartmentListItem>(x)).ToArray();
    }
}
