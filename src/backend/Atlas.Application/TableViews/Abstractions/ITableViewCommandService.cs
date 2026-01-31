using Atlas.Application.TableViews.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.TableViews.Abstractions;

public interface ITableViewCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        TableViewCreateRequest request,
        long id,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        TableViewUpdateRequest request,
        CancellationToken cancellationToken);

    Task UpdateConfigAsync(
        TenantId tenantId,
        long userId,
        long id,
        TableViewConfigUpdateRequest request,
        CancellationToken cancellationToken);

    Task<long> DuplicateAsync(
        TenantId tenantId,
        long userId,
        long id,
        TableViewDuplicateRequest request,
        long newId,
        CancellationToken cancellationToken);

    Task SetDefaultAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken);
}
