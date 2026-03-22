using Atlas.Application.Identity.Models;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using Atlas.Application.Platform.Abstractions;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class AppPermissionQueryService : IAppPermissionQueryService
{
    private readonly IAppPermissionRepository _repo;

    public AppPermissionQueryService(IAppPermissionRepository repo)
    {
        _repo = repo;
    }

    public async Task<PagedResult<PermissionListItem>> QueryAsync(
        TenantId tenantId,
        long appId,
        PermissionQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var (items, total) = await _repo.QueryPageAsync(
            tenantId,
            appId,
            pageIndex,
            pageSize,
            request.Keyword,
            request.Type,
            cancellationToken);
        var result = items.Select(x => new PermissionListItem(x.Id.ToString(), x.Name, x.Code, x.Type, x.Description)).ToArray();
        return new PagedResult<PermissionListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<PermissionDetail?> GetByIdAsync(
        TenantId tenantId,
        long appId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var x = await _repo.FindByIdAsync(tenantId, appId, id, cancellationToken);
        return x is null ? null : new PermissionDetail(x.Id.ToString(), x.Name, x.Code, x.Type, x.Description);
    }
}

public sealed class AppPermissionCommandService : IAppPermissionCommandService
{
    private readonly IAppPermissionRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;

    public AppPermissionCommandService(IAppPermissionRepository repo, IIdGeneratorAccessor idGen)
    {
        _repo = repo;
        _idGen = idGen;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long appId,
        PermissionCreateRequest request,
        long id,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.FindByCodeAsync(tenantId, appId, request.Code.Trim(), cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "AppPermissionCodeExists");
        }
        var entity = new AppPermission(tenantId, appId, request.Name.Trim(), request.Code.Trim(), request.Type.Trim(), id);
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            entity.Update(entity.Name, entity.Type, request.Description.Trim());
        }
        await _repo.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long appId,
        long id,
        PermissionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppScopedPermissionNotFound");
        entity.Update(request.Name.Trim(), request.Type.Trim(), request.Description?.Trim());
        await _repo.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long appId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repo.FindByIdAsync(tenantId, appId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "AppScopedPermissionNotFound");
        await _repo.DeleteAsync(tenantId, appId, entity.Id, cancellationToken);
    }
}
