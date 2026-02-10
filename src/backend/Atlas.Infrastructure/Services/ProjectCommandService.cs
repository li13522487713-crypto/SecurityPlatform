using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class ProjectCommandService : IProjectCommandService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IProjectDepartmentRepository _projectDepartmentRepository;
    private readonly IProjectPositionRepository _projectPositionRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectCommandService(
        IProjectRepository projectRepository,
        IProjectUserRepository projectUserRepository,
        IProjectDepartmentRepository projectDepartmentRepository,
        IProjectPositionRepository projectPositionRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _projectUserRepository = projectUserRepository;
        _projectDepartmentRepository = projectDepartmentRepository;
        _projectPositionRepository = projectPositionRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        ProjectCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var existing = await _projectRepository.FindByCodeAsync(tenantId, request.Code, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException("Project code already exists.", ErrorCodes.ValidationError);
        }

        var project = new Project(tenantId, request.Code, request.Name, id);
        project.Update(request.Name, request.Description, request.IsActive, request.SortOrder);
        await _projectRepository.AddAsync(project, cancellationToken);
        return project.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long id,
        ProjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (project is null)
        {
            throw new BusinessException("Project not found.", ErrorCodes.NotFound);
        }

        project.Update(request.Name, request.Description, request.IsActive, request.SortOrder);
        await _projectRepository.UpdateAsync(project, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var project = await _projectRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (project is null)
        {
            throw new BusinessException("Project not found.", ErrorCodes.NotFound);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _projectUserRepository.DeleteByProjectIdAsync(tenantId, id, cancellationToken);
            await _projectDepartmentRepository.DeleteByProjectIdAsync(tenantId, id, cancellationToken);
            await _projectPositionRepository.DeleteByProjectIdAsync(tenantId, id, cancellationToken);
            await _projectRepository.DeleteAsync(tenantId, id, cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateUsersAsync(
        TenantId tenantId,
        long id,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        await EnsureProjectExistsAsync(tenantId, id, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _projectUserRepository.DeleteByProjectIdAsync(tenantId, id, cancellationToken);
            if (userIds.Count > 0)
            {
                var items = userIds.Distinct()
                    .Select(userId => new ProjectUser(tenantId, id, userId, _idGeneratorAccessor.NextId()))
                    .ToArray();
                await _projectUserRepository.AddRangeAsync(items, cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task UpdateDepartmentsAsync(
        TenantId tenantId,
        long id,
        IReadOnlyList<long> departmentIds,
        CancellationToken cancellationToken)
    {
        await EnsureProjectExistsAsync(tenantId, id, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _projectDepartmentRepository.DeleteByProjectIdAsync(tenantId, id, cancellationToken);
            if (departmentIds.Count > 0)
            {
                var items = departmentIds.Distinct()
                    .Select(departmentId => new ProjectDepartment(tenantId, id, departmentId, _idGeneratorAccessor.NextId()))
                    .ToArray();
                await _projectDepartmentRepository.AddRangeAsync(items, cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task UpdatePositionsAsync(
        TenantId tenantId,
        long id,
        IReadOnlyList<long> positionIds,
        CancellationToken cancellationToken)
    {
        await EnsureProjectExistsAsync(tenantId, id, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _projectPositionRepository.DeleteByProjectIdAsync(tenantId, id, cancellationToken);
            if (positionIds.Count > 0)
            {
                var items = positionIds.Distinct()
                    .Select(positionId => new ProjectPosition(tenantId, id, positionId, _idGeneratorAccessor.NextId()))
                    .ToArray();
                await _projectPositionRepository.AddRangeAsync(items, cancellationToken);
            }
        }, cancellationToken);
    }

    private async Task EnsureProjectExistsAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (project is null)
        {
            throw new BusinessException("Project not found.", ErrorCodes.NotFound);
        }
    }
}
