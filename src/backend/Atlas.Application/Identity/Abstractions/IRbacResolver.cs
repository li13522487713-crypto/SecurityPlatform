using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Abstractions;

public interface IRbacResolver
{
    Task<IReadOnlyList<string>> GetRoleCodesAsync(
        UserAccount account,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);
}
