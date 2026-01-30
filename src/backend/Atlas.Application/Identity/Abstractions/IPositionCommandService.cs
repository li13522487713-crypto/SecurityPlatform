using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IPositionCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        PositionCreateRequest request,
        long id,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long positionId,
        PositionUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long positionId,
        CancellationToken cancellationToken);
}
