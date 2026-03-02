using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface IDictQueryService
{
    Task<PagedResult<DictTypeDto>> GetDictTypesPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DictTypeDto>> GetAllActiveDictTypesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<DictTypeDto?> GetDictTypeByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<PagedResult<DictDataDto>> GetDictDataPagedAsync(
        TenantId tenantId,
        string typeCode,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DictDataDto>> GetActiveDictDataByCodeAsync(
        TenantId tenantId,
        string typeCode,
        CancellationToken cancellationToken);
}
