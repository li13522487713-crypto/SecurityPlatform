using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class DepartmentCommandService : IDepartmentCommandService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IProjectDepartmentRepository _projectDepartmentRepository;
    private readonly Atlas.Core.Identity.IProjectContextAccessor _projectContextAccessor;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public DepartmentCommandService(
        IDepartmentRepository departmentRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IProjectDepartmentRepository projectDepartmentRepository,
        Atlas.Core.Identity.IProjectContextAccessor projectContextAccessor,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _projectDepartmentRepository = projectDepartmentRepository;
        _projectContextAccessor = projectContextAccessor;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        DepartmentCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var existing = await _departmentRepository.QueryAllAsync(tenantId, cancellationToken);
        if (existing.Any(x => x.Name == request.Name || x.Code == request.Code))
        {
            throw new BusinessException("Department name or code already exists.", ErrorCodes.Conflict);
        }

        var department = new Department(tenantId, request.Name, request.Code, id, request.ParentId, request.SortOrder);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _departmentRepository.AddAsync(department, cancellationToken);

            var projectContext = _projectContextAccessor.GetCurrent();
            if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
            {
                var link = new ProjectDepartment(
                    tenantId,
                    projectContext.ProjectId.Value,
                    department.Id,
                    _idGeneratorAccessor.NextId());
                await _projectDepartmentRepository.AddRangeAsync(new[] { link }, cancellationToken);
            }
        }, cancellationToken);
        return department.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long departmentId,
        DepartmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureDepartmentInProjectAsync(tenantId, departmentId, cancellationToken);
        
        var existingNodes = await _departmentRepository.QueryAllAsync(tenantId, cancellationToken);
        if (existingNodes.Any(x => x.Id != departmentId && (x.Name == request.Name || x.Code == request.Code)))
        {
            throw new BusinessException("Department name or code already exists.", ErrorCodes.Conflict);
        }

        var department = existingNodes.FirstOrDefault(x => x.Id == departmentId);
        if (department is null)
        {
            throw new BusinessException("Department not found.", ErrorCodes.NotFound);
        }

        department.Update(request.Name, request.Code, request.ParentId, request.SortOrder);
        await _departmentRepository.UpdateAsync(department, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
        await EnsureDepartmentInProjectAsync(tenantId, departmentId, cancellationToken);
        var department = await _departmentRepository.FindByIdAsync(tenantId, departmentId, cancellationToken);
        if (department is null)
        {
            throw new BusinessException("Department not found.", ErrorCodes.NotFound);
        }

        var hasChildren = await _departmentRepository.ExistsByParentIdAsync(tenantId, departmentId, cancellationToken);
        if (hasChildren)
        {
            throw new BusinessException("Department has child nodes.", ErrorCodes.ValidationError);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _userDepartmentRepository.DeleteByDepartmentIdAsync(tenantId, departmentId, cancellationToken);
            await _projectDepartmentRepository.DeleteByDepartmentIdAsync(tenantId, departmentId, cancellationToken);
            await _departmentRepository.DeleteAsync(tenantId, departmentId, cancellationToken);
        }, cancellationToken);
    }

    private async Task EnsureDepartmentInProjectAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken)
    {
        var projectContext = _projectContextAccessor.GetCurrent();
        if (!projectContext.IsEnabled || !projectContext.ProjectId.HasValue)
        {
            return;
        }

        var relations = await _projectDepartmentRepository.QueryByProjectIdAsync(
            tenantId,
            projectContext.ProjectId.Value,
            cancellationToken);
        if (!relations.Any(x => x.DepartmentId == departmentId))
        {
            throw new BusinessException("Department not in current project.", ErrorCodes.Forbidden);
        }
    }
}
