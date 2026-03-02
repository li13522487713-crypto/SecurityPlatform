using Atlas.Application.System.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface IDictCommandService
{
    Task<long> CreateDictTypeAsync(
        TenantId tenantId,
        DictTypeCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateDictTypeAsync(
        TenantId tenantId,
        long id,
        DictTypeUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteDictTypeAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateDictDataAsync(
        TenantId tenantId,
        string typeCode,
        DictDataCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateDictDataAsync(
        TenantId tenantId,
        long id,
        DictDataUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteDictDataAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);
}
