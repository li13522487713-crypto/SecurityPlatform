using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Enums;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class AppOrgQueryService : IAppOrgQueryService
{
    private readonly IAppDepartmentRepository _deptRepo;
    private readonly IAppPositionRepository _posRepo;
    private readonly IAppProjectRepository _projRepo;

    public AppOrgQueryService(
        IAppDepartmentRepository deptRepo,
        IAppPositionRepository posRepo,
        IAppProjectRepository projRepo)
    {
        _deptRepo = deptRepo;
        _posRepo = posRepo;
        _projRepo = projRepo;
    }

    public async Task<PagedResult<AppDepartmentListItem>> QueryDepartmentsAsync(TenantId tenantId, long appId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var (items, total) = await _deptRepo.QueryPageAsync(tenantId, appId, pageIndex, pageSize, request.Keyword, cancellationToken);
        var result = items.Select(x => new AppDepartmentListItem(x.Id.ToString(), x.Name, x.Code, x.ParentId?.ToString(), x.SortOrder)).ToArray();
        return new PagedResult<AppDepartmentListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<AppDepartmentListItem>> GetAllDepartmentsAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var items = await _deptRepo.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        return items.Select(x => new AppDepartmentListItem(x.Id.ToString(), x.Name, x.Code, x.ParentId?.ToString(), x.SortOrder)).ToArray();
    }

    public async Task<AppDepartmentDetail?> GetDepartmentByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var x = await _deptRepo.FindByIdAsync(tenantId, appId, id, cancellationToken);
        return x is null ? null : new AppDepartmentDetail(x.Id.ToString(), x.AppId.ToString(), x.Name, x.Code, x.ParentId?.ToString(), x.SortOrder);
    }

    public async Task<PagedResult<AppPositionListItem>> QueryPositionsAsync(TenantId tenantId, long appId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var (items, total) = await _posRepo.QueryPageAsync(tenantId, appId, pageIndex, pageSize, request.Keyword, cancellationToken);
        var result = items.Select(x => new AppPositionListItem(x.Id.ToString(), x.Name, x.Code, x.Description, x.IsActive, x.SortOrder)).ToArray();
        return new PagedResult<AppPositionListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<AppPositionListItem>> GetAllPositionsAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var items = await _posRepo.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        return items.Select(x => new AppPositionListItem(x.Id.ToString(), x.Name, x.Code, x.Description, x.IsActive, x.SortOrder)).ToArray();
    }

    public async Task<AppPositionDetail?> GetPositionByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var x = await _posRepo.FindByIdAsync(tenantId, appId, id, cancellationToken);
        return x is null ? null : new AppPositionDetail(x.Id.ToString(), x.AppId.ToString(), x.Name, x.Code, x.Description, x.IsActive, x.SortOrder);
    }

    public async Task<PagedResult<AppProjectListItem>> QueryProjectsAsync(TenantId tenantId, long appId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var (items, total) = await _projRepo.QueryPageAsync(tenantId, appId, pageIndex, pageSize, request.Keyword, cancellationToken);
        var result = items.Select(x => new AppProjectListItem(x.Id.ToString(), x.Code, x.Name, x.Description, x.IsActive)).ToArray();
        return new PagedResult<AppProjectListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<AppProjectListItem>> GetAllProjectsAsync(TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var items = await _projRepo.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        return items.Select(x => new AppProjectListItem(x.Id.ToString(), x.Code, x.Name, x.Description, x.IsActive)).ToArray();
    }

    public async Task<AppProjectDetail?> GetProjectByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
    {
        var x = await _projRepo.FindByIdAsync(tenantId, appId, id, cancellationToken);
        return x is null ? null : new AppProjectDetail(x.Id.ToString(), x.AppId.ToString(), x.Code, x.Name, x.Description, x.IsActive);
    }
}

public sealed class AppOrgCommandService : IAppOrgCommandService
{
    private readonly IAppDepartmentRepository _deptRepo;
    private readonly IAppPositionRepository _posRepo;
    private readonly IAppProjectRepository _projRepo;
    private readonly IIdGeneratorAccessor _idGen;

    public AppOrgCommandService(
        IAppDepartmentRepository deptRepo,
        IAppPositionRepository posRepo,
        IAppProjectRepository projRepo,
        IIdGeneratorAccessor idGen)
    {
        _deptRepo = deptRepo;
        _posRepo = posRepo;
        _projRepo = projRepo;
        _idGen = idGen;
    }

    public async Task<long> CreateDepartmentAsync(TenantId tenantId, long appId, AppDepartmentCreateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new AppDepartment(tenantId, appId, request.Name.Trim(), request.Code.Trim(), request.ParentId, request.SortOrder, _idGen.NextId());
        await _deptRepo.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateDepartmentAsync(TenantId tenantId, long appId, long id, AppDepartmentUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _deptRepo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppOrgDepartmentNotFound");
        entity.Update(request.Name.Trim(), request.Code.Trim(), request.ParentId, request.SortOrder);
        await _deptRepo.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteDepartmentAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
        => await _deptRepo.DeleteAsync(tenantId, appId, id, cancellationToken);

    public async Task<long> CreatePositionAsync(TenantId tenantId, long appId, AppPositionCreateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new AppPosition(tenantId, appId, request.Name.Trim(), request.Code.Trim(), _idGen.NextId());
        entity.Update(request.Name.Trim(), request.Description, request.IsActive, request.SortOrder);
        await _posRepo.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdatePositionAsync(TenantId tenantId, long appId, long id, AppPositionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _posRepo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppOrgPositionNotFound");
        entity.Update(request.Name.Trim(), request.Description, request.IsActive, request.SortOrder);
        await _posRepo.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeletePositionAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
        => await _posRepo.DeleteAsync(tenantId, appId, id, cancellationToken);

    public async Task<long> CreateProjectAsync(TenantId tenantId, long appId, AppProjectCreateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new AppProject(tenantId, appId, request.Code.Trim(), request.Name.Trim(), _idGen.NextId());
        entity.Update(request.Name.Trim(), request.Description, request.IsActive);
        await _projRepo.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateProjectAsync(TenantId tenantId, long appId, long id, AppProjectUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _projRepo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppOrgProjectNotFound");
        entity.Update(request.Name.Trim(), request.Description, request.IsActive);
        await _projRepo.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteProjectAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken = default)
        => await _projRepo.DeleteAsync(tenantId, appId, id, cancellationToken);
}

public sealed class AppRoleAssignmentQueryService : IAppRoleAssignmentQueryService
{
    private readonly IAppRoleRepository _roleRepo;
    private readonly IAppRolePageRepository _rolePageRepo;
    private readonly IFieldPermissionRepository _fieldPermRepo;

    public AppRoleAssignmentQueryService(
        IAppRoleRepository roleRepo,
        IAppRolePageRepository rolePageRepo,
        IFieldPermissionRepository fieldPermRepo)
    {
        _roleRepo = roleRepo;
        _rolePageRepo = rolePageRepo;
        _fieldPermRepo = fieldPermRepo;
    }

    public async Task<AppRoleAssignmentDetail> GetRoleAssignmentAsync(TenantId tenantId, long appId, long roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepo.FindByIdAsync(tenantId, appId, roleId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppOrgRoleNotFound");
        var deptIds = string.IsNullOrWhiteSpace(role.DeptIds)
            ? Array.Empty<string>()
            : role.DeptIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return new AppRoleAssignmentDetail(role.Id.ToString(), role.Code, role.Name, (int)role.DataScope, deptIds);
    }

    public async Task<IReadOnlyList<long>> GetRolePagesAsync(TenantId tenantId, long appId, long roleId, CancellationToken cancellationToken = default)
    {
        return await _rolePageRepo.QueryPageIdsByRoleIdAsync(tenantId, appId, roleId, cancellationToken);
    }

    public async Task<IReadOnlyList<AppRoleFieldPermissionGroup>> GetRoleFieldPermissionsAsync(TenantId tenantId, long appId, long roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepo.FindByIdAsync(tenantId, appId, roleId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppOrgRoleNotFound");
        var records = await _fieldPermRepo.ListByRoleCodeAndAppIdAsync(tenantId, appId, role.Code, cancellationToken);
        var prefix = $"app:{appId}:";
        var groups = records
            .GroupBy(x => x.TableKey.StartsWith(prefix) ? x.TableKey[prefix.Length..] : x.TableKey)
            .Select(g => new AppRoleFieldPermissionGroup(
                g.Key,
                g.Select(f => new AppRoleFieldPermissionItem(f.FieldName, f.CanView, f.CanEdit)).ToArray()))
            .ToArray();
        return groups;
    }
}

public sealed class AppRoleAssignmentCommandService : IAppRoleAssignmentCommandService
{
    private readonly IAppRoleRepository _roleRepo;
    private readonly IAppRolePageRepository _rolePageRepo;
    private readonly IFieldPermissionRepository _fieldPermRepo;
    private readonly IAppDepartmentRepository _appDeptRepo;
    private readonly IIdGeneratorAccessor _idGen;

    public AppRoleAssignmentCommandService(
        IAppRoleRepository roleRepo,
        IAppRolePageRepository rolePageRepo,
        IFieldPermissionRepository fieldPermRepo,
        IAppDepartmentRepository appDeptRepo,
        IIdGeneratorAccessor idGen)
    {
        _roleRepo = roleRepo;
        _rolePageRepo = rolePageRepo;
        _fieldPermRepo = fieldPermRepo;
        _appDeptRepo = appDeptRepo;
        _idGen = idGen;
    }

    public async Task SetDataScopeAsync(TenantId tenantId, long appId, long roleId, AppRoleDataScopeRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepo.FindByIdAsync(tenantId, appId, roleId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppOrgRoleNotFound");
        var scope = (DataScopeType)request.DataScope;
        string? deptIds = null;
        if (scope == DataScopeType.CustomDept && request.DeptIds is { Count: > 0 })
        {
            var distinctDeptIds = request.DeptIds.Where(x => x > 0).Distinct().ToArray();
            if (distinctDeptIds.Length > 0)
            {
                var validDepts = await _appDeptRepo.QueryByIdsAsync(tenantId, appId, distinctDeptIds, cancellationToken);
                var invalidIds = distinctDeptIds.Except(validDepts.Select(d => d.Id)).ToArray();
                if (invalidIds.Length > 0)
                {
                    throw new BusinessException(ErrorCodes.ValidationError, "AppRoleDeptIdsInvalid");
                }
            }
            deptIds = string.Join(',', distinctDeptIds);
        }
        role.SetDataScope(scope, deptIds);
        await _roleRepo.UpdateAsync(role, cancellationToken);
    }

    public async Task SetRolePagesAsync(TenantId tenantId, long appId, long roleId, IReadOnlyList<long> pageIds, CancellationToken cancellationToken = default)
    {
        var distinctIds = pageIds.Distinct().ToArray();
        var newEntities = distinctIds.Select(pageId => new AppRolePage(tenantId, appId, roleId, pageId, _idGen.NextId())).ToArray();
        await _rolePageRepo.ReplaceAsync(tenantId, appId, roleId, distinctIds, newEntities, cancellationToken);
    }

    public async Task SetRoleFieldPermissionsAsync(TenantId tenantId, long appId, long roleId, AppRoleFieldPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepo.FindByIdAsync(tenantId, appId, roleId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppOrgRoleNotFound");
        var now = DateTimeOffset.UtcNow;
        var prefix = $"app:{appId}:";
        var permissions = request.Groups
            .SelectMany(g => g.Fields.Select(f => new FieldPermission(
                tenantId,
                $"{prefix}{g.TableKey}",
                f.FieldName,
                role.Code,
                f.CanView,
                f.CanEdit,
                _idGen.NextId(),
                now,
                appId)))
            .ToArray();
        await _fieldPermRepo.ReplaceByRoleCodeAndAppIdAsync(tenantId, appId, role.Code, permissions, cancellationToken);
    }
}
