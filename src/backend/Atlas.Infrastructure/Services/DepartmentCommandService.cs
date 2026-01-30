using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DepartmentCommandService : IDepartmentCommandService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly ISqlSugarClient _db;

    public DepartmentCommandService(
        IDepartmentRepository departmentRepository,
        IUserDepartmentRepository userDepartmentRepository,
        ISqlSugarClient db)
    {
        _departmentRepository = departmentRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _db = db;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        DepartmentCreateRequest request,
        long id,
        CancellationToken cancellationToken)
    {
        var department = new Department(tenantId, request.Name, id, request.ParentId, request.SortOrder);
        await _departmentRepository.AddAsync(department, cancellationToken);
        return department.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long departmentId,
        DepartmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.FindByIdAsync(tenantId, departmentId, cancellationToken);
        if (department is null)
        {
            throw new BusinessException("Department not found.", ErrorCodes.NotFound);
        }

        department.Update(request.Name, request.ParentId, request.SortOrder);
        await _departmentRepository.UpdateAsync(department, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
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

        await _db.Ado.UseTranAsync(async () =>
        {
            await _userDepartmentRepository.DeleteByDepartmentIdAsync(tenantId, departmentId, cancellationToken);
            await _departmentRepository.DeleteAsync(tenantId, departmentId, cancellationToken);
        });
    }
}
