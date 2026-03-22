using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class PermissionQueryService : IPermissionQueryService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMapper _mapper;

    public PermissionQueryService(IPermissionRepository permissionRepository, IMapper mapper)
    {
        _permissionRepository = permissionRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<PermissionListItem>> QueryPermissionsAsync(
        PermissionQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _permissionRepository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            request.Type,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<PermissionListItem>(x)).ToArray();
        return new PagedResult<PermissionListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<PermissionDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var permission = await _permissionRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (permission is null)
        {
            return null;
        }

        return _mapper.Map<PermissionDetail>(permission);
    }
}
