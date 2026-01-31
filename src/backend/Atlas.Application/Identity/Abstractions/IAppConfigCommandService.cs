using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IAppConfigCommandService
{
    Task UpdateAsync(TenantId tenantId, long id, AppConfigUpdateRequest request, CancellationToken cancellationToken);
}
