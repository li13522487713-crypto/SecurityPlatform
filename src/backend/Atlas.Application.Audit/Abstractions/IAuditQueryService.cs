using Atlas.Application.Audit.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Audit.Abstractions;

public interface IAuditQueryService
{
    Task<PagedResult<AuditListItem>> QueryAuditsAsync(
        PagedRequest request,
        TenantId tenantId,
        string? action,
        string? result,
        CancellationToken cancellationToken);
}