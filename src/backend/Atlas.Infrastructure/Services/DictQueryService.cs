using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class DictQueryService : IDictQueryService
{
    private readonly DictTypeRepository _dictTypeRepository;
    private readonly DictDataRepository _dictDataRepository;

    public DictQueryService(DictTypeRepository dictTypeRepository, DictDataRepository dictDataRepository)
    {
        _dictTypeRepository = dictTypeRepository;
        _dictDataRepository = dictDataRepository;
    }

    public async Task<PagedResult<DictTypeDto>> GetDictTypesPagedAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await _dictTypeRepository.GetPagedAsync(tenantId, keyword, pageIndex, pageSize, cancellationToken);
        var dtos = items.Select(x => new DictTypeDto(x.Id, x.Code, x.Name, x.Status, x.Remark)).ToList();
        return new PagedResult<DictTypeDto>(dtos, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<DictTypeDto>> GetAllActiveDictTypesAsync(
        TenantId tenantId, CancellationToken cancellationToken)
    {
        var items = await _dictTypeRepository.GetAllActiveAsync(tenantId, cancellationToken);
        return items.Select(x => new DictTypeDto(x.Id, x.Code, x.Name, x.Status, x.Remark)).ToList();
    }

    public async Task<DictTypeDto?> GetDictTypeByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var item = await _dictTypeRepository.FindByIdAsync(tenantId, id, cancellationToken);
        return item is null ? null : new DictTypeDto(item.Id, item.Code, item.Name, item.Status, item.Remark);
    }

    public async Task<PagedResult<DictDataDto>> GetDictDataPagedAsync(
        TenantId tenantId, string typeCode, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await _dictDataRepository.GetPagedByTypeCodeAsync(
            tenantId, typeCode, keyword, pageIndex, pageSize, cancellationToken);
        var dtos = items.Select(Map).ToList();
        return new PagedResult<DictDataDto>(dtos, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<DictDataDto>> GetActiveDictDataByCodeAsync(
        TenantId tenantId, string typeCode, CancellationToken cancellationToken)
    {
        var items = await _dictDataRepository.GetActiveByTypeCodeAsync(tenantId, typeCode, cancellationToken);
        return items.Select(Map).ToList();
    }

    private static DictDataDto Map(Atlas.Domain.System.Entities.DictData x)
        => new(x.Id, x.DictTypeCode, x.Label, x.Value, x.SortOrder, x.Status, x.CssClass, x.ListClass);
}
